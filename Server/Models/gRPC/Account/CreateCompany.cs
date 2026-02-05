using Microsoft.EntityFrameworkCore.Query.Internal;
using ProtoBuf;

namespace Server.Models.gRPC.Account
{
    [ProtoContract]
    public class CreateCompanyRequest
    {
        [ProtoMember(1)] public string CreationToken;
    }
    public class CreateCompanyResponce
    {
        [ProtoMember(1)] public string Token;
    }
}
