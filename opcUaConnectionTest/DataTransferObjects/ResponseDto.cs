namespace PG.LIFT.Integrations.EMMS.DataTransferObjects
{
    using Opc.Ua;
    using Opc.Ua.Client;
    using Opc.Ua.Configuration;

    public sealed class ResponseDto : IEncodeable
    {
        public ushort Error { get; set; }
        public int Status { get; set; }

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
}
