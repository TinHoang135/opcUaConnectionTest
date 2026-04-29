namespace opcUaConnectionTest.DataTransferObjects
{
    public sealed class WallECobotProgramRequestDto
    {
        public required string IntegrationVehicleId { get; set; }

        // Int Parameter
        public int Task { get; init; }

        // Float Parameter
        public float CoreWeight { get; init; }
        public float CoreDiameter { get; init; }

        public int Strategy { get; init; }

        public int[] GetIntArray() => [Task, 0, 0, 0, 0];

        public float[] GetFloatArray() => [CoreWeight, CoreDiameter, 0, 0, 0];

        public bool[] GetBoolArray() => [false, false, false, false, false];
    }
}
