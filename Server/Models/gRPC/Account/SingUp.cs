using ProtoBuf;

namespace Server.Models.gRPC.Account
{
    [ProtoContract]
    public class SignUpResponce
    {
    }

    [ProtoContract]
    public class SignUpRequest
    {
        [ProtoMember(1)]
        public string SignUpToken {  get; set; } = string.Empty;

        [ProtoMember(2)]
        public string Name { get; set; } = string.Empty;

        [ProtoMember(3)]
        public string Surname { get; set; } = string.Empty;

        [ProtoMember(4)]
        public string Email { get; set; } = string.Empty;

        [ProtoMember(5)]
        public string PublicKey { get; set; } = string.Empty;
    }
}
