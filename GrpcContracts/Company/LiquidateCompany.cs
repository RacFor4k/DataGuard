using ProtoBuf;

namespace GrpcContracts.Company
{
    [ProtoContract]
    public class LiquidateCompanyRequest
    {
        [ProtoMember(1)] public string Token { get; set; }
        [ProtoMember(2)] public LiquidateCompanyStatus Type { get; set; }
    }

    [ProtoContract]
    public class LiquidateCompanyConfirmRequest
    {
        [ProtoMember(1)] public int key { get; set; }
    }
    
    [ProtoContract]
    public class LiquidateCompanyResponce
    {
    }
    
    public enum LiquidateCompanyStatus
    {
        Active,
        Closed,
        Archived,
        Initial,
    }
}
