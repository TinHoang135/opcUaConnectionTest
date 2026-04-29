using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using System.Text;

using opcUaConnectionTest.DataTransferObjects;

namespace opcUaConnectionTest.OPC
{
    public class OpcUaTester
    {
        #region Fields

        private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _maxDelay = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _reconcileDelay = TimeSpan.FromSeconds(10);

        private IOpcUaConnectionManager _opcUaConnectionManager;

        private ILoggerFactory _loggerFactory;
        private ILogger<OpcUaTester> _logger;

        #endregion Fields

        #region Properties

        public OpcUaApplication OpcUaApplication { get; set; }

        #endregion Properties

        #region Constructors

        public OpcUaTester(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<OpcUaTester>();

            OpcUaApplication = LoadOpcUaApplication();
            _opcUaConnectionManager = new OpcUaConnectionManager(OpcUaApplication, _loggerFactory.CreateLogger<OpcUaConnectionManager>(), _loggerFactory);
        }

        #endregion Constructors

        #region Methods

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var delay = _initialDelay;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _opcUaConnectionManager.InitializeAsync(cancellationToken);
                    _logger.LogInformation("OPC UA connection manager initialized successfully.");
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OPC UA initialization failed. Retrying in {Seconds}s.", delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, _maxDelay.TotalSeconds));
                }
            }

            await ExecuteWallECobotProgram(cancellationToken);
            await OpcUaConnectionTestEVE(cancellationToken);
        }

        private static OpcUaApplication LoadOpcUaApplication()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            return configuration.GetSection("OpcUa").Get<OpcUaApplication>()
                ?? throw new InvalidOperationException("OpcUa section is missing from appsettings.json.");
        }

        private static string DumpDataValue(DataValue? dataValue)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== OPC UA DataValue Dump ===");

            if (dataValue == null)
            {
                sb.AppendLine("DataValue is null.");
            }
            else
            {
                sb.AppendLine($"Status Code: {dataValue.StatusCode}");
                sb.AppendLine($"Source Timestamp: {dataValue.SourceTimestamp.ToString()}");
                sb.AppendLine($"Server Timestamp: {dataValue.ServerTimestamp.ToString()}");
                sb.AppendLine($"Source Picoseconds: {dataValue.SourcePicoseconds}");
                sb.AppendLine($"Server Picoseconds: {dataValue.ServerPicoseconds}");
                sb.AppendLine($"Value Type: {dataValue.Value.GetType().FullName}");
                sb.AppendLine($"Value ToString: {dataValue.Value.ToString()}");
                sb.AppendLine($"Value: {dataValue.Value}");
                sb.AppendLine($"Wrapped Value Type: {dataValue.WrappedValue.GetType().FullName}");
                sb.AppendLine($"Wrapped Value ToString: {dataValue.WrappedValue.ToString()}");
                sb.AppendLine($"Wrapped Value: {dataValue.WrappedValue}");
            }

            sb.AppendLine("=== End of DataValue Dump ===");
            return sb.ToString();
        }

        public async Task ExecuteWallECobotProgram(CancellationToken cancellationToken = default)
        {
            // TODO: Replace these values with meaningful test values.
            var request = new WallECobotProgramRequestDto
            {
                IntegrationVehicleId = "M5_WALL-E",
                CoreWeight = 12.5f,
                CoreDiameter = 5.0f,
                Strategy = 1
            };

            var opcServerConfiguration = OpcUaApplication.Servers.FirstOrDefault(server => server.IntegrationVehicleId.Equals("M5_WALL-E")) ?? throw new Exception("OPC Server configuration for 'M5_WALL-E' not found.");
            var requestBaseNode = opcServerConfiguration.CobotProgramRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var responseBaseNode = opcServerConfiguration.CobotProgramResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

            _logger.LogInformation($"Vehicle ID: {opcServerConfiguration.IntegrationVehicleId}.");

            const int errorStatus = 0;
            const int readyStatus = 1;
            const int activeStatus = 2;
            const int doneStatus = 3;
            int failedReadAttempt = 0;
            DataValue? statusDataValue;
            DataValue? errorDataValue;

            try
            {
                // Read current state
                // Try a few times before throwing exception
                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    statusDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}");
                    _logger.LogInformation(DumpDataValue(statusDataValue));

                    // errorDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}.Error");
                    // int? initialStatus = statusDataValue != null ? OpcInteraction.TryConvertToInt32(statusDataValue) : null;
                    // int? initialError = errorDataValue != null ? OpcInteraction.TryConvertToInt16(errorDataValue) : null;

                    int? initialStatus = null;
                    int? initialError = null;

                    // Assert readiness
                    if (initialStatus.HasValue && initialError.HasValue && initialStatus.Value == readyStatus && initialError.Value == 0)
                    {
                        _logger.LogInformation($"Status: {initialStatus}, Error: {initialError}.");
                        break;
                    }

                    else
                    {
                        if (failedReadAttempt >= 3)
                        {
                            if (!initialStatus.HasValue || !initialError.HasValue) throw new Exception("Failed to read cobot response state.");
                            else if (initialStatus.Value != readyStatus) throw new Exception($"Cobot not ready. Current status: {initialStatus.Value}.");
                            else if (initialError.Value != 0) throw new Exception($"Cobot reported error before execution. Error code: {initialError.Value}.");
                            else throw new Exception("Unknown error.");
                        }
                        else failedReadAttempt++;
                    }
                    // throttle
                    await Task.Delay(1000, cancellationToken);
                }

                // TODO: Writes and OPC program execution commented out until reads are confirmed working.

                /*
                // Write request parameters
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}.Strategy", request.Strategy, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}.RobSkillParametersInts", request.GetIntArray(), cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}.RobSkillParametersFloats", request.GetFloatArray(), cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}.RobSkillParametersBools", request.GetBoolArray(), cancellationToken);

                // Trigger execution by providing a lead edge to Exec bit
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}.Exec", false, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}.Exec", true, cancellationToken);

                // Monitor execution via response nodes until done or error
                int? statusValue;
                int? errorValue;
                bool operationStarted = false;
                failedReadAttempt = 0;

                while (!cancellationToken.IsCancellationRequested)
                {

                    cancellationToken.ThrowIfCancellationRequested();

                    statusDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}.Status");
                    errorDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}.Error");

                    statusValue = statusDataValue != null ? OpcInteraction.TryConvertToInt32(statusDataValue) : null;
                    errorValue = errorDataValue != null ? OpcInteraction.TryConvertToInt16(errorDataValue) : null;

                    if (statusValue.HasValue && errorValue.HasValue)
                    {
                        failedReadAttempt = 0;
                        if (statusValue == doneStatus) break;
                        else if (statusValue == errorStatus) throw new Exception($"Cobot execution error: {errorValue}");
                        else if (statusValue == activeStatus)
                        {
                            operationStarted = true;
                        }
                        else if (statusValue == readyStatus)
                        {
                            if (operationStarted) throw new Exception("Unexpected Cobot status: Cobot returns to Ready status during operation");
                            else { }
                        }
                        else throw new Exception($"Unknown Cobot status: {statusValue}");

                    }

                    else
                    {
                        if (failedReadAttempt >= 3) throw new Exception("Failed reading cobot response state.");
                        else failedReadAttempt++;
                    }

                    // Throttle
                    await Task.Delay(1000, cancellationToken);
                }
                */
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OPC UA interaction.");
            }

            // TODO: Writes commented out until reads are confirmed working.
            /*
            finally
            {
                // Reset execution trigger bit
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/Exec", false, cancellationToken);

                // Reset state to be ready for next execution
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{responseBaseNode}/Error", 0, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{responseBaseNode}/Status", readyStatus, cancellationToken);
            }
            */
        }

        public async Task OpcUaConnectionTestEVE(CancellationToken cancellationToken = default)
        {
            var opcServerConfiguration = OpcUaApplication.Servers.FirstOrDefault(server => server.IntegrationVehicleId == "EVE_Gen2_000001") ?? throw new Exception("OPC Server configuration for EVE not found.");
            var requestBaseNode = opcServerConfiguration.CobotProgramRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var responseBaseNode = opcServerConfiguration.CobotProgramResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

            Console.WriteLine($"Vehicle ID: {opcServerConfiguration.IntegrationVehicleId}.");

            int ReadAttempt = 0;
            DataValue? opcUaDataValue;

            try
            {
                // Read current state
                // Try a few times before throwing exception
                while (ReadAttempt < 3)
                {
                    // TODO: For debugging purposes only.
                    opcUaDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}");
                    Console.WriteLine(DumpDataValue(opcUaDataValue));

                    // throttle
                    await Task.Delay(2000);
                    ReadAttempt++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading OPC UA node: {ex}");
            }
        }

        #endregion Methods
    }
}
