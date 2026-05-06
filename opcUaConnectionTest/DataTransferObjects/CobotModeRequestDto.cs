using opcUaConnectionTest.DataTransferObjects;

namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    public sealed class CobotModeRequestDto
    {
        public required string IntegrationVehicleId { get; set; }

        // Int Parameters
        public int CobotMode { get; init; }

    }

    public static class CobotModeRequestDtoExtensions
    {
        private const int ExecOffset = 0;                                      // 0
        private const int CobotModeOffset = ExecOffset + sizeof(bool);         // 1
        public const int PayloadSize = CobotModeOffset + sizeof(int);          // 5

        public static byte[] ToByteArrayExecFalse(this CobotModeRequestDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var bytes = new byte[PayloadSize];

            // Exec
            bytes[ExecOffset] = (byte)0;

            // Mode
            BitConverter.GetBytes(dto.CobotMode).CopyTo(bytes, CobotModeOffset);

            return bytes;
        }

        public static void SetExecTrue(this byte[] bytes)
        {
            // Exec to True
            bytes[ExecOffset] = (byte)1;
        }

        public static CobotModeRequestDto ToCobotModeRequestDto(
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

            return new CobotModeRequestDto
            {
                IntegrationVehicleId = integrationVehicleId,
                CobotMode = BitConverter.ToInt32(bytes, CobotModeOffset)
            };
        }
    }
}
