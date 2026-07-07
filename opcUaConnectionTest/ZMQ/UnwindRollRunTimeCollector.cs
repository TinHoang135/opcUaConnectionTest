using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace unwindRollRuntime.ZMQ
{
    public class UnwindRollRunTimeCollector : IAsyncDisposable
    {
        #region Fields

        private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _maxDelay = TimeSpan.FromMinutes(1);

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<UnwindRollRunTimeCollector> _logger;

        #endregion Fields

        #region Properties

        public OpcUaApplication OpcUaApplication { get; set; }

        #endregion Properties

        #region Constructors

        public UnwindRollRunTimeCollector(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<UnwindRollRunTimeCollector>();

            OpcUaApplication = LoadOpcUaApplication();
            _opcUaConnectionManager = new OpcUaConnectionManager(OpcUaApplication, _loggerFactory.CreateLogger<OpcUaConnectionManager>(), _loggerFactory);
        }

        #endregion Constructors

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            if (_opcUaConnectionManager is IAsyncDisposable disposable)
            {
                _logger.LogInformation("Disposing OPC UA connection manager and closing sessions...");
                await disposable.DisposeAsync();
            }
        }

        #endregion IAsyncDisposable

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

            await OpcUaConnectionTestWallE(cancellationToken);
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
                /*
                sb.AppendLine($"Source Timestamp: {dataValue.SourceTimestamp.ToString()}");
                sb.AppendLine($"Server Timestamp: {dataValue.ServerTimestamp.ToString()}");
                sb.AppendLine($"Source Picoseconds: {dataValue.SourcePicoseconds}");
                sb.AppendLine($"Server Picoseconds: {dataValue.ServerPicoseconds}");
                */
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

        public async Task OpcUaConnectionTestWallE(CancellationToken cancellationToken = default)
        {
            var opcServerConfiguration = OpcUaApplication.Servers.FirstOrDefault(server => server.IntegrationVehicleId.Equals("M5_WALL-E")) ?? throw new Exception("OPC Server configuration for 'M5_WALL-E' not found.");
            var progRequestBaseNode = opcServerConfiguration.CobotProgramRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var progResponseBaseNode = opcServerConfiguration.CobotProgramResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            var modeRequestBaseNode = opcServerConfiguration.CobotModeRequestBaseNodeId ?? throw new InvalidOperationException("Cobot ack request base node ID is null.");
            var modeResponseBaseNode = opcServerConfiguration.CobotModeResponseBaseNodeId ?? throw new InvalidOperationException("Cobot ack response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

            Console.WriteLine($"Vehicle ID: {opcServerConfiguration.IntegrationVehicleId}.");

            int ReadAttempt = 0;
            DataValue? opcUaProgResponseData;
            DataValue? opcUaProgRequestData;
            DataValue? opcUaModeResponseData;
            DataValue? opcUaModeRequestData;

            // Read current state
            // Try a few times before exit
            while (ReadAttempt < 3)
            {
                try
                {
                    // Read OPC UA nodes
                    opcUaProgResponseData = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{progResponseBaseNode}");
                    Console.WriteLine($"Wall-e program response node data: {DumpDataValue(opcUaProgResponseData)}");
                    // decode the data
                    if (opcUaProgResponseData.Value is ExtensionObject progResponseExtensionObject)
                    {
                        if (progResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            Console.WriteLine($"Status: {decoded.Status}");
                            Console.WriteLine($"Error: {decoded.Error}");
                        }
                    }
                    
                    opcUaProgRequestData = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{progRequestBaseNode}");
                    Console.WriteLine($"Wall-e program request node data: {DumpDataValue(opcUaProgRequestData)}");
                    // decode the data
                    if (opcUaProgRequestData.Value is ExtensionObject progRequestExtensionObject)
                    {
                        if (progRequestExtensionObject.Body is byte[] debugBytes)
                        {
                            WallECobotProgramRequestDto decoded = debugBytes.ToWallECobotProgramRequestDto(opcServerConfiguration.IntegrationVehicleId);
                            Console.WriteLine($"Task: {decoded.Task}");
                            Console.WriteLine($"Core Weight: {decoded.CoreWeight}");
                            Console.WriteLine($"Core Diameter: {decoded.CoreDiameter}");
                            Console.WriteLine($"Strategy: {decoded.Strategy}");
                        }
                    }

                    opcUaModeRequestData = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{modeRequestBaseNode}");
                    Console.WriteLine($"Wall-e ack request node data: {DumpDataValue(opcUaModeRequestData)}");
                    //decode data
                    if (opcUaModeRequestData.Value is ExtensionObject modeRequestExtensionObject)
                    {
                        if (modeRequestExtensionObject.Body is byte[] debugBytes)
                        {
                            CobotModeRequestDto decoded = debugBytes.ToCobotModeRequestDto(opcServerConfiguration.IntegrationVehicleId);
                            Console.WriteLine($"Cobot Mode: {decoded.CobotMode}");
                        }
                    }

                    opcUaModeResponseData = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{modeResponseBaseNode}");
                    Console.WriteLine($"Wall-e response node data: {DumpDataValue(opcUaModeResponseData)}");
                    // decode the data
                    if (opcUaModeResponseData.Value is ExtensionObject modeResponseExtensionObject)
                    {
                        if (modeResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            Console.WriteLine($"Status: {decoded.Status}");
                            Console.WriteLine($"Error: {decoded.Error}");
                        }
                    }

                    break;
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading OPC UA node: {ex}");
                    // throttle
                    await Task.Delay(2000);
                    ReadAttempt++;
                }
            }
        }
        #endregion Methods
    }
}
