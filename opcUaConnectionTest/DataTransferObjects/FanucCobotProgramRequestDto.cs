namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    public sealed class FanucCobotProgramRequestDto
    {
        public required string IntegrationVehicleId { get; set; }

        // Int Parameter
        public int Station { get; init; }
        public int Task { get; init; }
        public int TransferPoint { get; init; }

        // Float Parameter
        public float CoreWeight { get; init; }
        public float CoreDiameter { get; init; }

        public int Strategy { get; init; }

        public int[] GetIntArray() => [Station, Task, TransferPoint, 0, 0];

        public float[] GetFloatArray() => [CoreWeight, CoreDiameter, 0, 0, 0];

        public bool[] GetBoolArray() => [false, false, false, false, false];
    }
}
