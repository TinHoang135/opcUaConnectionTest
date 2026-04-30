namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    public sealed class TowerProgramRequestDto
    {
        public required string IntegrationVehicleId { get; set; }

        // Int Parameters
        public int Station { get; init; }
        public int Task { get; init; }
        public int TransferPoint { get; init; }

        // Float Parameter
        public float Weight { get; init; }
        public float Depth { get; init; }
        public float InnerDiameter { get; init; }
        public float OuterDiameter { get; init; }

    }
}
