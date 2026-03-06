using ProtoBuf;

namespace GrpcContracts.Account
{
    [ProtoContract]
    public class RefreshTokenRequest
    {
        [ProtoMember(1)] public string RefreshToken { get; set; } = string.Empty;
    }

    [ProtoContract]
    public class RefreshTokenResponce
    {
        [ProtoMember(1)] public string AccessToken { get; set; } = string.Empty;
        [ProtoMember(2)] public string RefreshToken { get; set; } = string.Empty;
    }
}
