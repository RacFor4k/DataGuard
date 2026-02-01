using ProtoBuf;

namespace Server.Models.gRPC
{
    [ProtoContract]
    public class RequestHeader
    {

    }

    [ProtoContract]
    public class ResponceHeader
    {
        [ProtoMember(1)]
        public int Status;
    }
}
