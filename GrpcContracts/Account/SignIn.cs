using ProtoBuf;
using System.Security.Cryptography.X509Certificates;

namespace GrpcContracts.Account
{
    [ProtoContract]
    public class SignInResponce
    {
        [ProtoMember(1)] public string Name { get; set; } = "";
        [ProtoMember(2)] public string Surname { get; set; } = "";
        [ProtoMember(3)] public string Email { get; set; } = "";
        [ProtoMember(4)] public string EncryptedToken { get; set; } = "";
        [ProtoMember(5)] public string AccessToken { get; set; } = string.Empty;
        [ProtoMember(6)] public string RefreshToken {  get; set; } = string.Empty;
    }

    [ProtoContract]
    public class SignInRequest
    {
        [ProtoMember(1)] public int UserId { get; set; }
        [ProtoMember(2)] public string Nonce { get; set; } = "";
        [ProtoMember(3)] public string Signature { get; set; } = "";

    }
}
