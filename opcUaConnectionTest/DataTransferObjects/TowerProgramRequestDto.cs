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

    public static class TowerProgramRequestDtoExtensions
    {
        private const int ExecOffset = 0;                                      // 0
        private const int WeightOffset = ExecOffset + sizeof(bool);            // 1
        private const int DepthOffset = WeightOffset + sizeof(float);          // 5
        private const int StationOffset = DepthOffset + sizeof(float);         // 9
        private const int TaskOffset = StationOffset + sizeof(int);            // 13
        private const int TransferPointOffset = TaskOffset + sizeof(int);      // 17
        private const int InnerDiameterOffset = TransferPointOffset + sizeof(int);   // 21
        private const int OuterDiameterOffset = InnerDiameterOffset + sizeof(float); // 25
        public const int PayloadSize = OuterDiameterOffset + sizeof(float);          // 29

        public static byte[] ToByteArrayExecFalse(this TowerProgramRequestDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var bytes = new byte[PayloadSize];

            // Exec
            bytes[ExecOffset] = (byte)0;

            // Weight
            BitConverter.GetBytes(dto.Weight).CopyTo(bytes, WeightOffset);

            // Depth
            BitConverter.GetBytes(dto.Depth).CopyTo(bytes, DepthOffset);

            // Station
            BitConverter.GetBytes(dto.Station).CopyTo(bytes, StationOffset);

            // Task
            BitConverter.GetBytes(dto.Task).CopyTo(bytes, TaskOffset);

            // Transfer Point
            BitConverter.GetBytes(dto.TransferPoint).CopyTo(bytes, TransferPointOffset);

            // Inner Diameter
            BitConverter.GetBytes(dto.InnerDiameter).CopyTo(bytes, InnerDiameterOffset);

            // Outer Diameter
            BitConverter.GetBytes(dto.OuterDiameter).CopyTo(bytes, OuterDiameterOffset);

            return bytes;
        }

        public static void SetExecTrue(this byte[] bytes)
        {
            // Exec to True
            bytes[ExecOffset] = (byte)1;
        }

        public static TowerProgramRequestDto ToTowerProgramRequestDto(
            this byte[] bytes,
            string integrationVehicleId)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            ArgumentNullException.ThrowIfNull(integrationVehicleId);
            if (bytes.Length != PayloadSize)
            {
                throw new ArgumentException(
                    $"Buffer must be exactly {PayloadSize} bytes.", nameof(bytes));
            }

            return new TowerProgramRequestDto
            {
                IntegrationVehicleId = integrationVehicleId,
                Weight = BitConverter.ToInt32(bytes, WeightOffset),
                Depth = BitConverter.ToInt32(bytes, DepthOffset),
                Station = BitConverter.ToInt32(bytes, StationOffset),
                Task = BitConverter.ToInt32(bytes, TaskOffset),
                TransferPoint = BitConverter.ToInt32(bytes, TransferPointOffset),
                InnerDiameter = BitConverter.ToInt32(bytes, InnerDiameterOffset),
                OuterDiameter = BitConverter.ToInt32(bytes, OuterDiameterOffset)
            };
        }
    }
}
