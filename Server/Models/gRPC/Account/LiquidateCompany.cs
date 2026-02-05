using Microsoft.EntityFrameworkCore.Query.Internal;
using ProtoBuf;

namespace Server.Models.gRPC.Account
{
    [ProtoContract]
    public class LiquidateCompanyRequest
    {
        [ProtoMember(1)] public string Token { get; set; }
        [ProtoMember(2)] public Models.Db.Identity.Status Type { get; set; }
    }

    [ProtoContract]
    public class LiquidateCompanyConfirmRequest
    {
        [ProtoMember(1)] public int key { get; set; }
    }
    public class LiquidateCompanyResponce
    {
    }
}
