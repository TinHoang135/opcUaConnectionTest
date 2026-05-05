namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    using Opc.Ua;
    using Opc.Ua.Client;
    using Opc.Ua.Configuration;

    public sealed class ResponseDto : IEncodeable
    {
        public int Status { get; set; }
        public ushort Error { get; set; }
        
        public ExpandedNodeId TypeId => null;
        public ExpandedNodeId BinaryEncodingId => null;
        public ExpandedNodeId XmlEncodingId => null;

        public void Encode(IEncoder encoder)
        {
            encoder.WriteInt32("Status", Status);
            encoder.WriteUInt16("Error", Error);
        }

        public void Decode(IDecoder decoder)
        {
            Status = decoder.ReadInt32("Status");
            Error = decoder.ReadUInt16("Error");
        }

        public bool IsEqual(IEncodeable encodeable)
        {
            var other = encodeable as ResponseDto;
            if (other == null) return false;
            return Status == other.Status && Error == other.Error;
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }

    /**
    byte:   0    1    2    3    4    5
        ┌────┬────┬────┬────┬────┬────┐
        │      Status       │  Error  │
        └────┴────┴────┴────┴────┴────┘
*/
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
}
