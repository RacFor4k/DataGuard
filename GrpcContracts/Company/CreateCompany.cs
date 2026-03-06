using ProtoBuf;

namespace GrpcContracts.Company
{
    [ProtoContract]
    public class CreateCompanyRequest
    {
        [ProtoMember(1)] public string CreationToken;
    }
    
    [ProtoContract]
    public class CreateCompanyResponce
    {
        [ProtoMember(1)] public string Token;
    }
}
