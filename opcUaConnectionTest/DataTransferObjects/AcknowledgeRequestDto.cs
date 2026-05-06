namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    public sealed class AcknowledgeRequestDto
    {
        public required string IntegrationVehicleId { get; set; }

    }

    public static class AcknowledgeRequestDtoExtensions
    {
        private const int ExecOffset = 0;                                  // 0
        public const int PayloadSize = ExecOffset + sizeof(bool);          // 1

        public static byte[] ToByteArrayExecFalse(this AcknowledgeRequestDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var bytes = new byte[PayloadSize];

            // Exec
            bytes[ExecOffset] = (byte)0;

            return bytes;
        }

        public static void SetExecTrue(this byte[] bytes)
        {
            // Exec to True
            bytes[ExecOffset] = (byte)1;
        }

        public static AcknowledgeRequestDto ToAcknowledgeRequestDto(
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

            return new AcknowledgeRequestDto
            {
                IntegrationVehicleId = integrationVehicleId
            };
        }
    }
}
