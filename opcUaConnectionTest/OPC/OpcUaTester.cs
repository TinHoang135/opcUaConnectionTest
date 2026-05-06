using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using opcUaConnectionTest.DataTransferObjects;
using PG.LIFT.Integrations.EMMS.DataTransferObjects;

namespace opcUaConnectionTest.OPC
{
    public class OpcUaTester
    {
        #region Fields

        private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _maxDelay = TimeSpan.FromMinutes(1);
        // private readonly TimeSpan _reconcileDelay = TimeSpan.FromSeconds(10);

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

            await OpcUaConnectionTestWallE(cancellationToken);
            await ExecuteWallECobotProgram(cancellationToken);
            //await OpcUaConnectionTestEVE(cancellationToken);
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

        public async Task OpcUaConnectionTestEVE(CancellationToken cancellationToken = default)
        {
            var opcServerConfiguration = OpcUaApplication.Servers.FirstOrDefault(server => server.IntegrationVehicleId == "EVE_Gen2_000001") ?? throw new Exception("OPC Server configuration for EVE not found.");
            var CobotProgRequestBaseNode = opcServerConfiguration.CobotProgramRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var CobotProgResponseBaseNode = opcServerConfiguration.CobotProgramResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            var CobotAckRequestBaseNode = opcServerConfiguration.CobotAcknowledgeRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var CobotAckResponseBaseNode = opcServerConfiguration.CobotAcknowledgeResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            var TowerRequestBaseNode = opcServerConfiguration.TowerProgramRequestBaseNodeId ?? throw new InvalidOperationException("Tower program request base node ID is null.");
            var TowerResponseBaseNode = opcServerConfiguration.TowerProgramResponseBaseNodeId ?? throw new InvalidOperationException("Tower program response base node ID is null.");
            var MuteRequestBaseNode = opcServerConfiguration.SafetyZoneMuteRequestBaseNodeId ?? throw new InvalidOperationException("Mute request base node ID is null.");
            var MuteResponseBaseNode = opcServerConfiguration.SafetyZoneMuteResponseBaseNodeId ?? throw new InvalidOperationException("Mute response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

            Console.WriteLine($"Vehicle ID: {opcServerConfiguration.IntegrationVehicleId}.");

            int ReadAttempt = 0;
            DataValue? opcUaDataFanucProgResponse;
            DataValue? opcUaDataFanucProgRequest;
            DataValue? opcUaDataFanucAckResponse;
            DataValue? opcUaDataFanucAckRequest;
            DataValue? opcUaDataTowerResponse;
            DataValue? opcUaDataTowerRequest;
            DataValue? opcUaDataMuteResponse;
            DataValue? opcUaDataMuteRequest;

            // Read current state
            // Try a few times before exit
            while (ReadAttempt < 3)
            {
                try
                {
                    // Read OPC UA nodes
                    opcUaDataFanucProgRequest = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{CobotProgRequestBaseNode}");
                    Console.WriteLine($"EVE Cobot Program Request data: {DumpDataValue(opcUaDataFanucProgRequest)}");
                    // decode the data
                    if (opcUaDataFanucProgRequest.Value is ExtensionObject progRequestExtensionObject)
                    {
                        if (progRequestExtensionObject.Body is byte[] debugBytes)
                        {
                            FanucCobotProgramRequestDto decoded = debugBytes.ToFanucCobotProgramRequestDto(opcServerConfiguration.IntegrationVehicleId);
                            Console.WriteLine($"Station: {decoded.Station}");
                            Console.WriteLine($"Task:  {decoded.Task}");
                            Console.WriteLine($"Transfer Point: {decoded.TransferPoint}");
                            Console.WriteLine($"Core Weight:  {decoded.CoreWeight}");
                            Console.WriteLine($"Core Diameter:  {decoded.CoreDiameter}");
                        }
                    }

                    opcUaDataFanucProgResponse = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{CobotProgResponseBaseNode}");
                    Console.WriteLine($"EVE Cobot Program Response data: {DumpDataValue(opcUaDataFanucProgResponse)}");
                    // decode the data
                    if (opcUaDataFanucProgResponse.Value is ExtensionObject progResponseExtensionObject)
                    {
                        if (progResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            Console.WriteLine($"Status: {decoded.Status}");
                            Console.WriteLine($"Error: {decoded.Error}");
                        }
                    }

                    opcUaDataFanucAckRequest = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{CobotAckRequestBaseNode}");
                    Console.WriteLine($"EVE Cobot Ack Request data: {DumpDataValue(opcUaDataFanucAckRequest)}");
                    // decode the data
                    if (opcUaDataFanucAckRequest.Value is ExtensionObject ackRequestExtensionObject)
                    {
                        if (ackRequestExtensionObject.Body is byte[] debugBytes)
                        {
                            AcknowledgeRequestDto decoded = debugBytes.ToAcknowledgeRequestDto(opcServerConfiguration.IntegrationVehicleId);
                        }
                    }

                    opcUaDataFanucAckResponse = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{CobotAckResponseBaseNode}");
                    Console.WriteLine($"EVE Cobot Ack Response data: {DumpDataValue(opcUaDataFanucAckResponse)}");
                    // decode the data
                    if (opcUaDataFanucAckResponse.Value is ExtensionObject ackResponseExtensionObject)
                    {
                        if (ackResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            Console.WriteLine($"Status: {decoded.Status}");
                            Console.WriteLine($"Error: {decoded.Error}");
                        }
                    }

                    opcUaDataTowerRequest = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{TowerRequestBaseNode}");
                    Console.WriteLine($"EVE Tower Request data: {DumpDataValue(opcUaDataTowerRequest)}");
                    if (opcUaDataTowerRequest.Value is ExtensionObject TowerRequestExtensionObject)
                    {
                        if (TowerRequestExtensionObject.Body is byte[] debugBytes)
                        {
                            TowerProgramRequestDto decoded = debugBytes.ToTowerProgramRequestDto(opcServerConfiguration.IntegrationVehicleId);
                            Console.WriteLine($"Weight: {decoded.Weight}");
                            Console.WriteLine($"Roll Depth: {decoded.Depth}");
                            Console.WriteLine($"Station: {decoded.Station}");
                            Console.WriteLine($"Task: {decoded.Task}");
                            Console.WriteLine($"Transfer Point: {decoded.TransferPoint}");
                            Console.WriteLine($"Roll Inner Diameter: {decoded.InnerDiameter}");
                            Console.WriteLine($"Roll Outer Diameter: {decoded.OuterDiameter}");
                        }
                    }

                    opcUaDataTowerResponse = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{TowerResponseBaseNode}");
                    Console.WriteLine($"EVE Tower Response data: {DumpDataValue(opcUaDataTowerResponse)}");
                    // decode the data
                    if (opcUaDataTowerResponse.Value is ExtensionObject TowerResponseExtensionObject)
                    {
                        if (TowerResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            Console.WriteLine($"Status: {decoded.Status}");
                            Console.WriteLine($"Error: {decoded.Error}");
                        }
                    }

                    opcUaDataMuteRequest = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{MuteRequestBaseNode}");
                    Console.WriteLine($"Mute Request data: {DumpDataValue(opcUaDataMuteRequest)}");
                    if (opcUaDataMuteRequest.Value is ExtensionObject MuteRequestExtensionObject)
                    {
                        if (MuteRequestExtensionObject.Body is byte[] debugBytes)
                        {
                            SafetyZoneProgramRequestDto decoded = debugBytes.ToSafetyZoneProgramRequestDto(opcServerConfiguration.IntegrationVehicleId);
                            Console.WriteLine($"Mute: {decoded.Mute}");
                            Console.WriteLine($"Station: {decoded.Station}");
                            Console.WriteLine($"Task: {decoded.Task}");
                        }
                    }

                    opcUaDataMuteResponse = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{MuteResponseBaseNode}");
                    Console.WriteLine($"Mute Response data: {DumpDataValue(opcUaDataMuteResponse)}");
                    // decode the data
                    if (opcUaDataMuteResponse.Value is ExtensionObject MuteResponseExtensionObject)
                    {
                        if (MuteResponseExtensionObject.Body is byte[] debugBytes)
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

        protected async Task ExecuteFanucCobotProgram(CancellationToken cancellationToken = default)
        {
            // TODO: Replace these values with meaningful test values.
            var request = new FanucCobotProgramRequestDto
            {
                IntegrationVehicleId = "EVE_Gen2_000001",
                // Int Parameter
                Station = 14,
                Task = 310,
                TransferPoint = 1,
                // Float Parameter
                CoreWeight = 0.75f,
                CoreDiameter = 100f,
                Strategy = 0
            };

            var opcServerConfiguration = OpcUaApplication.Servers.FirstOrDefault(server => server.IntegrationVehicleId == request.IntegrationVehicleId) ?? throw new Exception("OPC Server configuration for 'EVE_Gen2_000001' not found.");
            var requestBaseNode = opcServerConfiguration.CobotProgramRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var responseBaseNode = opcServerConfiguration.CobotProgramResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

            const int errorStatus = 0;
            const int readyStatus = 1;
            const int activeStatus = 2;
            const int doneStatus = 3;
            int failedReadAttempt = 0;
            DataValue? opcUaDataFanucProgResponse;
            byte[] opcUaRequestData = request.ToByteArrayExecFalse();

            try
            {
                // Read current state
                // Try a few times before throwing exception
                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // get data from the response node
                    opcUaDataFanucProgResponse = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}");
                    // Console.WriteLine($"EVE Cobot Program Response data: {DumpDataValue(opcUaDataWallEProgResponse)}");
                    // decode the data
                    if (opcUaDataFanucProgResponse.Value is ExtensionObject progResponseExtensionObject)
                    {
                        if (progResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            Console.WriteLine($"Fanuc program response Status: {decoded.Status}");
                            Console.WriteLine($"Fanuc program response Error: {decoded.Error}");
                            // Assert readiness
                            if (decoded.Status == readyStatus && decoded.Error == 0)
                                break;
                            else throw new Exception($"Fanuc Cobot not ready for operation. Status: {decoded.Status}. Error: {decoded.Error}."); ;
                        }
                        else failedReadAttempt++;
                    }
                    else failedReadAttempt++;

                    if (failedReadAttempt >= 3)
                    {
                        throw new Exception("Failed reading Fanuc Cobot response data.");
                    }
                    await Task.Delay(1000, cancellationToken);
                }

                // Write request parameters
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}", opcUaRequestData, cancellationToken);

                // Trigger execution by providing a lead edge to Exec bit
                opcUaRequestData.SetExecTrue();
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}", opcUaRequestData, cancellationToken);
                
                // Monitor execution via response nodes until done or error
                bool operationStarted = false;
                failedReadAttempt = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // get data from the response node
                    opcUaDataFanucProgResponse = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}");
                    // decode the data
                    if (opcUaDataFanucProgResponse.Value is ExtensionObject progResponseExtensionObject)
                    {
                        if (progResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            failedReadAttempt = 0;
                            if (decoded.Status == activeStatus)
                            {
                                operationStarted = true;
                            }
                            else if (decoded.Status == readyStatus)
                            {
                                if (operationStarted) throw new Exception("Unexpected Fanuc Cobot status: Cobot returns to Ready status during operation");
                                else { }
                            }
                            else if (decoded.Status == doneStatus) break;
                            else if (decoded.Status == errorStatus) throw new Exception($"Fanuc Cobot execution error: {decoded.Error}");
                            else throw new Exception($"Unknown Fanuc Cobot status: {decoded.Status}");
                        }
                        else failedReadAttempt++;
                    }
                    else failedReadAttempt++;
                    
                    if (failedReadAttempt >= 3) throw new Exception("Failed reading Fanuc Cobot response data.");
                    // Throttle
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch
            {
                throw;
            }

            finally
            {
                // Reset execution trigger bit
                opcUaRequestData.SetExecFalse();
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}", opcUaRequestData, cancellationToken);

                // Reset state to be ready for next execution
                var resetResponseNode = new ResponseDto
                {
                    Status = readyStatus,
                    Error = 0
                };
                byte[] opcUaResponseData = resetResponseNode.ToByteArray();
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{responseBaseNode}", opcUaResponseData, cancellationToken);
            }
        }

        protected async Task ExecuteWallECobotProgram(CancellationToken cancellationToken = default)
        {
            // TODO: Replace these values with meaningful test values.
            var request = new WallECobotProgramRequestDto
            {
                IntegrationVehicleId = "M5_WALL-E",
                // Int Parameter
                Task = 1000,
                // Float Parameter
                CoreWeight = 0.75f,
                CoreDiameter = 100f,
                Strategy = 1
            };

            var opcServerConfiguration = OpcUaApplication.Servers.FirstOrDefault(server => server.IntegrationVehicleId == request.IntegrationVehicleId) ?? throw new Exception("OPC Server configuration for 'M5_WALL-E' not found.");
            var requestBaseNode = opcServerConfiguration.CobotProgramRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var responseBaseNode = opcServerConfiguration.CobotProgramResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

            const int errorStatus = 0;
            const int readyStatus = 1;
            const int activeStatus = 2;
            const int doneStatus = 3;
            int failedReadAttempt = 0;
            DataValue? opcUaDataWallEProgResponse;
            byte[] opcUaRequestData = request.ToByteArrayExecFalse();

            try
            {
                // Read current state
                // Try a few times before throwing exception
                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // get data from the response node
                    opcUaDataWallEProgResponse = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}");
                    // Console.WriteLine($"EVE Cobot Program Response data: {DumpDataValue(opcUaDataWallEProgResponse)}");
                    // decode the data
                    if (opcUaDataWallEProgResponse.Value is ExtensionObject progResponseExtensionObject)
                    {
                        if (progResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            Console.WriteLine($"WallE program response Status: {decoded.Status}");
                            Console.WriteLine($"WallE program response Error: {decoded.Error}");
                            // Assert readiness
                            if (decoded.Status == readyStatus && decoded.Error == 0)
                                break;
                            else throw new Exception($"WallE Cobot not ready for operation. Status: {decoded.Status}. Error: {decoded.Error}."); ;
                        }
                        else failedReadAttempt++;
                    }
                    else failedReadAttempt++;

                    if (failedReadAttempt >= 3)
                    {
                        throw new Exception("Failed reading WallE Cobot response data.");
                    }
                    await Task.Delay(1000, cancellationToken);
                }

                // Read the current request node to get the ExtensionObject TypeId (UDT structure)
                var currentRequestData = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{requestBaseNode}");
                if (currentRequestData.Value is not ExtensionObject currentExtObj)
                    throw new Exception("Request node does not contain an ExtensionObject.");

                // Write request parameters wrapped in an ExtensionObject matching the node's type
                var writeExtObj = new ExtensionObject(currentExtObj.TypeId, opcUaRequestData);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}", writeExtObj, cancellationToken);

                // Trigger execution by providing a lead edge to Exec bit
                // opcUaRequestData.SetExecTrue();
                var writeExtObjExec = new ExtensionObject(currentExtObj.TypeId, opcUaRequestData);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}", writeExtObjExec, cancellationToken);

                // Write request parameters
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}", opcUaRequestData, cancellationToken);

                // Trigger execution by providing a lead edge to Exec bit
                // opcUaRequestData.SetExecTrue();
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}", opcUaRequestData, cancellationToken);

                // Monitor execution via response nodes until done or error
                bool operationStarted = false;
                failedReadAttempt = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // get data from the response node
                    opcUaDataWallEProgResponse = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}");
                    // decode the data
                    if (opcUaDataWallEProgResponse.Value is ExtensionObject progResponseExtensionObject)
                    {
                        if (progResponseExtensionObject.Body is byte[] debugBytes)
                        {
                            ResponseDto decoded = debugBytes.ToResponseDto();
                            failedReadAttempt = 0;
                            if (decoded.Status == activeStatus)
                            {
                                operationStarted = true;
                            }
                            else if (decoded.Status == readyStatus)
                            {
                                if (operationStarted) throw new Exception("Unexpected WallE Cobot status: Cobot returns to Ready status during operation");
                                else { }
                            }
                            else if (decoded.Status == doneStatus) break;
                            else if (decoded.Status == errorStatus) throw new Exception($"WallE Cobot execution error: {decoded.Error}");
                            else throw new Exception($"Unknown WallE Cobot status: {decoded.Status}");
                        }
                        else failedReadAttempt++;
                    }
                    else failedReadAttempt++;

                    if (failedReadAttempt >= 3) throw new Exception("Failed reading WallE Cobot response data.");
                    // Throttle
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch
            {
                throw;
            }

            finally
            {
                // Reset execution trigger bit
                opcUaRequestData.SetExecFalse();
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}", opcUaRequestData, cancellationToken);

                // Reset state to be ready for next execution
                var resetResponseNode = new ResponseDto
                {
                    Status = readyStatus,
                    Error = 0
                };
                byte[] opcUaResponseData = resetResponseNode.ToByteArray();
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{responseBaseNode}", opcUaResponseData, cancellationToken);
            }
        }

        protected async Task ExecuteTowerProgram(CancellationToken cancellationToken = default)
        {
            // TODO: Replace these values with meaningful test values.
            var request = new TowerProgramRequestDto
            {
                IntegrationVehicleId = "EVE_Gen2_000001",
                // Int Parameters
                Station = 14,
                Task = 30,
                TransferPoint = 3,
                // Float Parameter
                Weight = 10,
                Depth = 90,
                InnerDiameter = 200,
                OuterDiameter = 1200
            };

            var opcServerConfiguration = OpcUaApplication.Servers.FirstOrDefault(server => server.IntegrationVehicleId == request.IntegrationVehicleId) ?? throw new Exception("OPC Server configuration for 'EVE_Gen2_000001' not found.");
            var requestBaseNode = opcServerConfiguration.TowerProgramRequestBaseNodeId ?? throw new InvalidOperationException("Tower program request base node ID is null.");
            var responseBaseNode = opcServerConfiguration.TowerProgramResponseBaseNodeId ?? throw new InvalidOperationException("Tower program response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

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

                    statusDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}/Status");
                    errorDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}/Error");
                    int? initialStatus = statusDataValue != null ? OpcInteraction.TryConvertToInt32(statusDataValue) : null;
                    int? initialError = errorDataValue != null ? OpcInteraction.TryConvertToInt16(errorDataValue) : null;

                    // Assert readiness
                    if (initialStatus.HasValue && initialError.HasValue && initialStatus.Value == readyStatus && initialError.Value == 0)
                        break;

                    else
                    {
                        if (failedReadAttempt >= 3)
                        {
                            if (!initialStatus.HasValue || !initialError.HasValue) throw new Exception("Failed to read tower response state.");
                            else if (initialStatus.Value != readyStatus) throw new Exception($"Tower not ready. Current status: {initialStatus.Value}.");
                            else if (initialError.Value != 0) throw new Exception($"Tower reported error before execution. Error code: {initialError.Value}.");
                            else throw new Exception("Unknown error.");
                        }
                        else failedReadAttempt++;
                    }
                    // throttle
                    await Task.Delay(1000, cancellationToken);
                }

                // Write request parameters
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/Depth", request.Depth, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/InnerDiameter", request.InnerDiameter, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/OuterDiameter", request.OuterDiameter, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/Station", request.Station, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/Task", request.Task, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/TransferPoint", request.TransferPoint, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/Weight", request.Weight, cancellationToken);

                // Trigger execution by providing a lead edge to Exec bit
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/Exec", false, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/Exec", true, cancellationToken);

                // Monitor execution via response nodes until done or error
                int? statusValue;
                int? errorValue;
                bool operationStarted = false;
                failedReadAttempt = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    statusDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}/Status");
                    errorDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}/Error");

                    statusValue = statusDataValue != null ? OpcInteraction.TryConvertToInt32(statusDataValue) : null;
                    errorValue = errorDataValue != null ? OpcInteraction.TryConvertToInt16(errorDataValue) : null;

                    if (statusValue.HasValue && errorValue.HasValue)
                    {
                        failedReadAttempt = 0;
                        // check status value
                        if (statusValue == doneStatus) break;
                        else if (statusValue == errorStatus) throw new Exception($"Tower execution error: {errorValue}");
                        else if (statusValue == activeStatus)
                        {
                            operationStarted = true;
                        }
                        else if (statusValue == readyStatus)
                        {
                            if (operationStarted) throw new Exception("Unexpected Tower status: Tower returns to Ready status during operation");
                            else { }
                        }
                        else throw new Exception($"Unkown Tower status: {statusValue}");
                    }

                    else
                    {
                        if (failedReadAttempt >= 3) throw new Exception("Failed reading Tower response state.");
                        else failedReadAttempt++;
                    }

                    // Throttle
                    await Task.Delay(1000, cancellationToken);
                }
            }

            catch
            {
                throw;
            }

            finally
            {
                // Reset execution trigger bit
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{requestBaseNode}/Exec", false, cancellationToken);

                // Reset state to be ready for next execution
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{responseBaseNode}/Error", 0, cancellationToken);
                await _opcUaConnectionManager.WriteNodeAsync(serverName, $"{responseBaseNode}/Status", readyStatus, cancellationToken);
            }
        }

        #endregion Methods
    }
}
