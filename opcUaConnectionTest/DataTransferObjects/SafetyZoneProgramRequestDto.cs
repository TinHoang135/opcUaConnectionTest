using opcUaConnectionTest.DataTransferObjects;

namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    public sealed class SafetyZoneProgramRequestDto
    {
        public required string IntegrationVehicleId { get; set; }

        // Bool Parameter
        public bool Mute { get; init; }

        // Int Parameters
        public int Station { get; init; }
        public int Task { get; init; }
    }

    public static class SafetyZoneProgramRequestDtoExtensions
    {
        private const int ExecOffset = 0;                                      // 0
        private const int MuteOffset = ExecOffset + sizeof(bool);              // 1
        private const int StationOffset = MuteOffset + sizeof(bool);           // 2
        private const int TaskOffset = StationOffset + sizeof(int);            // 6
        public const int PayloadSize = TaskOffset + sizeof(int);               // 10

        public static byte[] ToByteArrayExecFalse(this SafetyZoneProgramRequestDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var bytes = new byte[PayloadSize];

            // Exec
            bytes[ExecOffset] = (byte)0;

            // Mute
            bytes[MuteOffset] = dto.Mute ? (byte)1 : (byte)0;

            // Station
            BitConverter.GetBytes(dto.Station).CopyTo(bytes, StationOffset);

            // Task
            BitConverter.GetBytes(dto.Task).CopyTo(bytes, TaskOffset);

            return bytes;
        }

        public static SafetyZoneProgramRequestDto ToSafetyZoneProgramRequestDto(
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

            return new SafetyZoneProgramRequestDto
            {
                IntegrationVehicleId = integrationVehicleId,
                Mute = bytes[MuteOffset] == 1 ? true : false,
                Station = BitConverter.ToInt32(bytes, StationOffset),
                Task = BitConverter.ToInt32(bytes, TaskOffset)
            };
        }
    }
}
