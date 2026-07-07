namespace unwindRollRuntime.ZMQ
{
    public sealed class OpcUaApplication
    {
        public required string ApplicationName { get; set; }
        public required string ApplicationUri { get; set; }
        public bool AutoAcceptUntrustedCertificates { get; set; }
        public List<OpcUaServer> Servers { get; set; } = new();
    }

    public sealed class OpcUaServer
    {
        public required string IntegrationVehicleId { get; set; }
        public required string Name { get; set; }
        public required string EndpointUrl { get; set; }
        public required string SecurityPolicy { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public required string? CobotProgramRequestBaseNodeId { get; set; }
        public required string? CobotProgramResponseBaseNodeId { get; set; }
        public required string? CobotAcknowledgeRequestBaseNodeId { get; set; }
        public required string? CobotAcknowledgeResponseBaseNodeId { get; set; }
        public required string? CobotModeRequestBaseNodeId { get; set; }
        public required string? CobotModeResponseBaseNodeId { get; set; }
        public required string? TowerProgramRequestBaseNodeId { get; set; }
        public required string? TowerProgramResponseBaseNodeId { get; set; }
        public required string? SafetyZoneMuteRequestBaseNodeId { get; set; }
        public required string? SafetyZoneMuteResponseBaseNodeId { get; set; }
    }
}
