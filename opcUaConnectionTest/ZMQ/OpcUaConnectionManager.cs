using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

using ISession = Opc.Ua.Client.ISession;

using StatusCodes = Opc.Ua.StatusCodes;

namespace opcUaConnectionTest.OPC
{
    public interface IOpcUaConnectionManager
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<DataValue> ReadNodeAsync(string serverName, string nodeId);
        Task WriteNodeAsync(string serverName, string nodeId, object value, CancellationToken cancellationToken = default);
    }

    public sealed class OpcUaConnectionManager : IOpcUaConnectionManager, IAsyncDisposable
    {
        #region Nested Types

        private sealed class LoggerFactoryTelemetryContext : ITelemetryContext, IDisposable
        {
            private readonly string meterName = typeof(OpcUaConnectionManager).Assembly.GetName().Name ?? "OpcUaClient";
            private readonly string meterVersion = typeof(OpcUaConnectionManager).Assembly.GetName().Version?.ToString() ?? "1.0.0";

            public LoggerFactoryTelemetryContext(ILoggerFactory loggerFactory)
            {
                LoggerFactory = loggerFactory;
                ActivitySource = new ActivitySource(meterName, meterVersion);
            }

            public ILoggerFactory LoggerFactory { get; }

            public ActivitySource ActivitySource { get; }

            public Meter CreateMeter() => new(meterName, meterVersion);

            public void Dispose()
            {
                ActivitySource.Dispose();
            }
        }

        #endregion Nested Types

        private static readonly TimeSpan DefaultSessionTimeout = TimeSpan.FromMinutes(1);
        private const int DefaultDiscoverTimeoutMs = 15_000;

        private readonly OpcUaApplication opcUaApplication;
        private readonly ILogger<OpcUaConnectionManager> logger;
        private readonly ITelemetryContext telemetryContext;
        private readonly Func<OpcUaServer, CancellationToken, Task> connectServerAsync;

        private readonly ConcurrentDictionary<string, ISession> sessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, SessionReconnectHandler> reconnectHandlers = new(StringComparer.OrdinalIgnoreCase);

        private ApplicationConfiguration? applicationConfiguration;

        public OpcUaConnectionManager(OpcUaApplication options, ILogger<OpcUaConnectionManager> logger, ILoggerFactory loggerFactory)
        {
            opcUaApplication = options;
            this.logger = logger;
            telemetryContext = new LoggerFactoryTelemetryContext(loggerFactory);
            this.connectServerAsync = ConnectToServerAsync;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            applicationConfiguration ??= await BuildAndValidateApplicationConfigurationAsync(cancellationToken);

            foreach (var server in opcUaApplication.Servers)
            {
                if (IsServerConnectionHealthy(server.Name))
                    continue;

                try
                {
                    await connectServerAsync(server, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to connect to OPC UA server {ServerName}. Will retry on next initialization cycle.", server.Name);
                }
            }
        }

        public Task<DataValue> ReadNodeAsync(string serverName, string nodeId)
        {
            var session = GetSession(serverName);
            var parsedNodeId = ParseNodeIdOrThrow(nodeId);

            // Library provides ReadValueAsync(NodeId) on ISession.
            return session.ReadValueAsync(parsedNodeId);
        }

        public async Task WriteNodeAsync(string serverName, string nodeId, object value, CancellationToken cancellationToken = default)
        {
            var session = GetSession(serverName);
            var parsedNodeId = ParseNodeIdOrThrow(nodeId);

            var writeValue = new WriteValue
            {
                NodeId = parsedNodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            var writeValues = new WriteValueCollection { writeValue };

            var response = await session.WriteAsync(
                requestHeader: null,
                nodesToWrite: writeValues,
                ct: cancellationToken);

            if (response?.Results == null || response.Results.Count != 1)
                throw new ServiceResultException(StatusCodes.BadUnexpectedError, "OPC UA write returned an unexpected result.");

            if (StatusCode.IsBad(response.Results[0]))
                throw new ServiceResultException(response.Results[0]);
        }

        private static NodeId ParseNodeIdOrThrow(string nodeId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

            return NodeId.Parse(nodeId);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var reconnectHandler in reconnectHandlers.Values)
            {
                reconnectHandler.Dispose();
            }
            reconnectHandlers.Clear();

            foreach (var session in sessions.Values)
            {
                try { await session.CloseAsync(); } catch { }
                session.Dispose();
            }
            sessions.Clear();

            if (telemetryContext is IDisposable disposableTelemetry)
            {
                disposableTelemetry.Dispose();
            }
        }

        private ISession GetSession(string serverName)
        {
            if (!sessions.TryGetValue(serverName, out var session))
                throw new InvalidOperationException($"Server '{serverName}' not connected.");

            return session;
        }

        private bool IsServerConnectionHealthy(string serverName)
        {
            if (!sessions.TryGetValue(serverName, out var session))
                return false;

            try
            {
                return session.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static UserIdentity CreateUserIdentity(OpcUaServer serverOptions)
        {
            if (string.IsNullOrWhiteSpace(serverOptions.UserName))
                return new UserIdentity();

            byte[] passwordBytes =
                string.IsNullOrEmpty(serverOptions.Password)
                    ? Array.Empty<byte>()
                    : Encoding.UTF8.GetBytes(serverOptions.Password);

            // Required: password as byte[] (UserIdentity overload supports this).
            return new UserIdentity(serverOptions.UserName, passwordBytes);
        }

        private async Task ConnectToServerAsync(OpcUaServer server, CancellationToken cancellationToken)
        {
            if (applicationConfiguration is null)
                throw new InvalidOperationException("OPC UA manager is not initialized.");

            if (sessions.TryRemove(server.Name, out var existingSession))
            {
                try { await existingSession.CloseAsync(cancellationToken); } catch { }
                existingSession.Dispose();
            }

            if (reconnectHandlers.TryRemove(server.Name, out var existingReconnectHandler))
            {
                existingReconnectHandler.Dispose();
            }

            var endpointUrl = server.EndpointUrl;
            bool useSecurity = !string.IsNullOrWhiteSpace(server.SecurityPolicy) &&
                !string.Equals(server.SecurityPolicy.Trim(), "None", StringComparison.OrdinalIgnoreCase);

            var selectedEndpoint = await CoreClientUtils.SelectEndpointAsync(
                applicationConfiguration,
                endpointUrl,
                useSecurity,
                DefaultDiscoverTimeoutMs,
                telemetryContext,
                cancellationToken);

            if (selectedEndpoint is null)
                throw new InvalidOperationException($"No suitable OPC UA endpoint found for {endpointUrl}.");

            var endpointConfiguration = EndpointConfiguration.Create(applicationConfiguration);
            var configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            IUserIdentity userIdentity = CreateUserIdentity(server);

            var sessionFactory = new DefaultSessionFactory(telemetryContext);

            var session = await sessionFactory.CreateAsync(
                configuration: applicationConfiguration,
                endpoint: configuredEndpoint,
                updateBeforeConnect: false,
                checkDomain: true,
                sessionName: server.Name,
                sessionTimeout: (uint)DefaultSessionTimeout.TotalMilliseconds,
                identity: userIdentity,
                preferredLocales: new List<string>(),
                ct: cancellationToken);

            session.KeepAliveInterval = 10_000;
            session.KeepAlive += (_, e) => OnKeepAlive(server.Name, session, e);

            sessions[server.Name] = session;

            logger.LogInformation(
                "Connected to OPC UA server {ServerName} at {EndpointUrl} with {SecurityPolicy} {SecurityMode}.",
                server.Name,
                selectedEndpoint.EndpointUrl,
                selectedEndpoint.SecurityPolicyUri,
                selectedEndpoint.SecurityMode);
        }

        private void OnKeepAlive(string serverName, ISession session, KeepAliveEventArgs e)
        {
            if (ServiceResult.IsGood(e.Status))
                return;

            logger.LogWarning("OPC UA keep-alive bad for {ServerName}: {Status}", serverName, e.Status);

            if (!reconnectHandlers.TryGetValue(serverName, out var reconnectHandler))
            {
                reconnectHandler = new SessionReconnectHandler(telemetryContext, reconnectAbort: true);
                reconnectHandlers[serverName] = reconnectHandler;
            }

            if (reconnectHandler.State != SessionReconnectHandler.ReconnectState.Ready)
                return;

            logger.LogInformation("Starting OPC UA reconnect for {ServerName}.", serverName);

            reconnectHandler.BeginReconnect(
                session: session,
                reconnectPeriod: 5_000,
                callback: (_, _) => OnReconnectComplete(serverName));
        }

        private void OnReconnectComplete(string serverName)
        {
            if (!reconnectHandlers.TryGetValue(serverName, out var reconnectHandler))
                return;

            try
            {
                var reconnectedSession = reconnectHandler.Session;
                reconnectHandler.CancelReconnect();

                if (reconnectedSession is null)
                {
                    logger.LogWarning("OPC UA reconnect callback returned null session for {ServerName}.", serverName);
                    return;
                }

                sessions[serverName] = reconnectedSession;

                logger.LogInformation("OPC UA reconnect complete for {ServerName}.", serverName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OPC UA reconnect failed for {ServerName}.", serverName);
            }
        }

        private async Task<ApplicationConfiguration> BuildAndValidateApplicationConfigurationAsync(CancellationToken cancellationToken)
        {
            // Immediate startup fix:
            // ApplicationConfiguration.Validate() requires StorePath for all trust lists, including TrustedIssuerCertificates.
            // Using directory-based stores under app base directory works reliably (including containers).
            var pkiRoot = Path.Combine(AppContext.BaseDirectory, "pki");

            var ownStorePath = Path.Combine(pkiRoot, "own");
            var trustedPeerStorePath = Path.Combine(pkiRoot, "trusted", "peer");
            var trustedIssuerStorePath = Path.Combine(pkiRoot, "trusted", "issuer");
            var rejectedStorePath = Path.Combine(pkiRoot, "rejected");

            var configuration = new ApplicationConfiguration
            {
                ApplicationName = opcUaApplication.ApplicationName,
                ApplicationUri = opcUaApplication.ApplicationUri,
                ApplicationType = ApplicationType.Client,

                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = ownStorePath,
                        SubjectName = opcUaApplication.ApplicationName
                    },

                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = trustedPeerStorePath
                    },

                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = trustedIssuerStorePath
                    },

                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = rejectedStorePath
                    },

                    AutoAcceptUntrustedCertificates = opcUaApplication.AutoAcceptUntrustedCertificates,
                    AddAppCertToTrustedStore = true,
                    RejectSHA1SignedCertificates = true,
                    MinimumCertificateKeySize = 2048
                },

                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 15_000
                },

                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = (int)DefaultSessionTimeout.TotalMilliseconds
                },

                DisableHiResClock = true
            };

            configuration.CertificateValidator = new CertificateValidator(telemetryContext);
            await configuration.CertificateValidator.UpdateAsync(configuration);

            configuration.CertificateValidator.CertificateValidation += (_, e) =>
            {
                if (opcUaApplication.AutoAcceptUntrustedCertificates)
                {
                    logger.LogWarning(
                        "Auto-accepting untrusted server certificate: {Subject} ({Thumbprint})",
                        e.Certificate?.Subject,
                        e.Certificate?.Thumbprint);

                    e.Accept = true;
                    return;
                }

                logger.LogError(
                    "Rejected untrusted server certificate: {Subject} ({Thumbprint}). Add it to: {TrustedStorePath}",
                    e.Certificate?.Subject,
                    e.Certificate?.Thumbprint,
                    trustedPeerStorePath);
            };

            await configuration.ValidateAsync(ApplicationType.Client);

            var applicationInstance = new ApplicationInstance(telemetryContext)
            {
                ApplicationName = configuration.ApplicationName,
                ApplicationType = configuration.ApplicationType,
                ApplicationConfiguration = configuration
            };

            bool certificateOk = await applicationInstance.CheckApplicationInstanceCertificatesAsync(silent: false);

            if (!certificateOk)
                throw new InvalidOperationException("Failed to create or load the OPC UA application certificate.");

            return configuration;
        }
    }
}
