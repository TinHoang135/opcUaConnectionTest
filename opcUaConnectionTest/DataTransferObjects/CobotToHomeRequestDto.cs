using opcUaConnectionTest.DataTransferObjects;

namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    public sealed class CobotToHomeRequestDto
    {
        public required string IntegrationVehicleId { get; set; }

        public int[] GetIntArray() => [0, 0, 0, 0, 0];

        public float[] GetFloatArray() => [0, 0, 0, 0, 0];

        public bool[] GetBoolArray() => [false, false, false, false, false];
    }

    public static class CobotToHomeRequestDtoExtensions
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

        public static byte[] ToByteArrayExecFalse(this CobotToHomeRequestDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var bytes = new byte[PayloadSize];
            int Strategy = 2;  // Home Cobot

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
            BitConverter.GetBytes(Strategy).CopyTo(bytes, StrategyOffset);

            return bytes;
        }

        public static void SetExecTrue(this byte[] bytes)
        {
            // Exec to True
            bytes[ExecOffset] = (byte)1;
        }
    }
}
