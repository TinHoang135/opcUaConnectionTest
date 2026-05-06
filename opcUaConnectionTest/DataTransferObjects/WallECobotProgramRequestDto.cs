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

    public static class WallECobotProgramRequestDtoExtensions
    {
        private const int BoolCount = 5;
        private const int IntCount = 5;
        private const int FloatCount = 5;

        private const int ExecOffset = 0;                                              // 0
        private const int BoolsLengthOffset = ExecOffset + sizeof(bool);              // 1
        private const int BoolsOffset = BoolsLengthOffset + sizeof(int);               // 5
        private const int IntsLengthOffset = BoolsOffset + (BoolCount * sizeof(bool));   // 10
        private const int IntsOffset = IntsLengthOffset + sizeof(int);               // 14
        private const int FloatsLengthOffset = IntsOffset + (IntCount * sizeof(int));    // 34
        private const int FloatsOffset = FloatsLengthOffset + sizeof(int);               // 38
        private const int StrategyOffset = FloatsOffset + (FloatCount * sizeof(float));  // 58
        public const int PayloadSize = StrategyOffset + sizeof(int);               // 62

        public static byte[] ToByteArrayExecFalse(this WallECobotProgramRequestDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var bytes = new byte[PayloadSize];

            // Exec
            bytes[ExecOffset] = (byte)0;

            // Bools (length-prefixed)
            BitConverter.GetBytes(BoolCount).CopyTo(bytes, BoolsLengthOffset);
            var offset = BoolsOffset;
            foreach (var b in dto.GetBoolArray())
            {
                bytes[offset++] = b ? (byte)1 : (byte)0;
            }

            // Ints (length-prefixed)
            BitConverter.GetBytes(IntCount).CopyTo(bytes, IntsLengthOffset);
            offset = IntsOffset;
            foreach (var i in dto.GetIntArray())
            {
                BitConverter.GetBytes(i).CopyTo(bytes, offset);
                offset += sizeof(int);
            }

            // Floats (length-prefixed)
            BitConverter.GetBytes(FloatCount).CopyTo(bytes, FloatsLengthOffset);
            offset = FloatsOffset;
            foreach (var f in dto.GetFloatArray())
            {
                BitConverter.GetBytes(f).CopyTo(bytes, offset);
                offset += sizeof(float);
            }

            // Strategy
            BitConverter.GetBytes(dto.Strategy).CopyTo(bytes, StrategyOffset);

            return bytes;
        }

        public static void SetExecTrue(this byte[] bytes)
        {
            // Exec to True
            bytes[ExecOffset] = (byte)1;
        }

        public static WallECobotProgramRequestDto ToWallECobotProgramRequestDto(
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

            // Length prefixes are skipped — fixed shape, but validate them so we fail
            // loudly if the upstream UDT ever changes size.
            ValidateArrayLength(bytes, BoolsLengthOffset, BoolCount, nameof(BoolCount));
            ValidateArrayLength(bytes, IntsLengthOffset, IntCount, nameof(IntCount));
            ValidateArrayLength(bytes, FloatsLengthOffset, FloatCount, nameof(FloatCount));

            return new WallECobotProgramRequestDto
            {
                IntegrationVehicleId = integrationVehicleId,
                Task = BitConverter.ToInt32(bytes, IntsOffset),                    // ints[0]
                CoreWeight = BitConverter.ToSingle(bytes, FloatsOffset),                  // floats[0]
                CoreDiameter = BitConverter.ToSingle(bytes, FloatsOffset + sizeof(float)),  // floats[1]
                Strategy = BitConverter.ToInt32(bytes, StrategyOffset),
            };
        }

        private static void ValidateArrayLength(byte[] bytes, int offset, int expected, string name)
        {
            var actual = BitConverter.ToInt32(bytes, offset);
            if (actual != expected)
            {
                throw new InvalidDataException(
                    $"Expected {name}={expected} but payload reports {actual} at offset {offset}.");
            }
        }
    }
}
