namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    public sealed class ResponseDto
    {
        public int Status { get; set; }
        public ushort Error { get; set; }
    }

    /**
    byte:   0    1    2    3    4    5
        ┌────┬────┬────┬────┬────┬────┐
        │      Status       │  Error  │
        └────┴────┴────┴────┴────┴────┘
    */

    public static class RequestDtoExtensions
    {
        private const int StatusOffset = 0;                                 // 0
        private const int ErrorOffset = StatusOffset + sizeof(int);         // 4
        public const int PayloadSize = ErrorOffset + sizeof(short);         // 6

        public static ResponseDto ToResponseDto(this byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (bytes.Length != PayloadSize)
            {
                throw new ArgumentException(
                    $"Buffer must be exactly {PayloadSize} bytes.", nameof(bytes));
            }
            return new ResponseDto
            {
                Status = BitConverter.ToInt32(bytes, 0),   // bytes 0..3
                Error = BitConverter.ToUInt16(bytes, 4)    // bytes 4..5
            };
        }

        public static byte[] ToByteArray(this ResponseDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var bytes = new byte[PayloadSize];

            // Status
            BitConverter.GetBytes(dto.Status).CopyTo(bytes, StatusOffset);

            // Error
            BitConverter.GetBytes(dto.Error).CopyTo(bytes, ErrorOffset);

            return bytes;
        }
    }

    /*
    public static class ByteArrayExtensions
    {
        public static ResponseDto ToResponseDto(this byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (bytes.Length != 6)
            {
                throw new ArgumentException("Buffer must be exactly 6 bytes.", nameof(bytes));
            }

            return new ResponseDto
            {
                Status = BitConverter.ToInt32(bytes, 0),   // bytes 0..3
                Error = BitConverter.ToUInt16(bytes, 4),  // bytes 4..5
            };
        }
    }
    */
}
