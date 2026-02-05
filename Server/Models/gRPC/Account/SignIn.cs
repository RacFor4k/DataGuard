using ProtoBuf;

namespace Server.Models.gRPC.Account
{
    [ProtoContract]
    public class SignInResponce
    {
        [ProtoMember(1)] public string Name { get; set; } = "";
        [ProtoMember(2)] public string Surname { get; set; } = "";
        [ProtoMember(3)] public string Email { get; set; } = "";
        [ProtoMember(4)] public string EncryptedToken { get; set; } = "";
        [ProtoMember(5)] public List<KeyValuePair<int, string>> Groups { get; set; } = new List<KeyValuePair<int, string>>();
        [ProtoMember(6)] public string SeccionId { get; set; } = string.Empty;
    }

    [ProtoContract]
    public class SignInRequest
    {
        [ProtoMember(1)] public string PublicKey { get; set; } = "";
    }
}
