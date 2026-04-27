using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using opcUaConnectionTest.OPC;

namespace opcUaConnectionTest.Services
{
    internal class OpcUaConnectionTestService
    {
        private readonly IOptions<OpcUaApplication> _opcUaApplication;
        private readonly IOpcUaConnectionManager _opcUaConnectionManager;

        public OpcUaConnectionTestService (
            IOptions<OpcUaApplication> opcUaApplication, 
            IOpcUaConnectionManager opcUaConnectionManager)
        {
            _opcUaApplication = opcUaApplication;
            _opcUaConnectionManager = opcUaConnectionManager;
        }

        public async Task OpcUaConnectionTestWallE()
        {
            var opcServerConfiguration = _opcUaApplication.Value.Servers.FirstOrDefault(server => server.IntegrationVehicleId == "M5_WALL-E") ?? throw new Exception("OPC Server configuration for WallE not found.");
            var requestBaseNode = opcServerConfiguration.CobotProgramRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var responseBaseNode = opcServerConfiguration.CobotProgramResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

            _logger.LogInformation($"Vehicle ID: {opcServerConfiguration.IntegrationVehicleId}.");

            int ReadAttempt = 0;
            DataValue? opcUaDataValue;

            try
            {
                // Read current state
                // Try a few times before throwing exception
                while (ReadAttempt < 5)
                {

                    // TODO: For debugging purposes only.
                    opcUaDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}");
                    _logger.LogInformation(DumpDataValue(opcUaDataValue));

                    // throttle
                    await Task.Delay(2000);
                    ReadAttempt ++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation ($"Error reading OPC UA node: {ex}");
            }
        }

        public async Task OpcUaConnectionTestEVE()
        {
            var opcServerConfiguration = _opcUaApplication.Value.Servers.FirstOrDefault(server => server.IntegrationVehicleId == "EVE_Gen2_000001") ?? throw new Exception("OPC Server configuration for EVE not found.");
            var requestBaseNode = opcServerConfiguration.CobotProgramRequestBaseNodeId ?? throw new InvalidOperationException("Cobot program request base node ID is null.");
            var responseBaseNode = opcServerConfiguration.CobotProgramResponseBaseNodeId ?? throw new InvalidOperationException("Cobot program response base node ID is null.");
            string serverName = opcServerConfiguration.Name;

            _logger.LogInformation($"Vehicle ID: {opcServerConfiguration.IntegrationVehicleId}.");

            int ReadAttempt = 0;
            DataValue? opcUaDataValue;

            try
            {
                // Read current state
                // Try a few times before throwing exception
                while (ReadAttempt < 5)
                {

                    // TODO: For debugging purposes only.
                    opcUaDataValue = await _opcUaConnectionManager.ReadNodeAsync(serverName, $"{responseBaseNode}");
                    _logger.LogInformation(DumpDataValue(opcUaDataValue));

                    // throttle
                    await Task.Delay(2000);
                    ReadAttempt++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error reading OPC UA node: {ex}");
            }
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
    }
}
