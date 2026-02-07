using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using Server.Models.gRPC.Account;

namespace Server.Contracts
{
    public class AccountContract
    {
        [Service]
        public interface IAccountServise
        {
            ValueTask<SignUpResponce> SignUp(SignUpRequest request, CallContext context = default);
            ValueTask<SignInResponce> SignIn(SignInRequest request, CallContext context = default);

            ValueTask<CreateCompanyResponce> CreateCompany(CreateCompanyRequest request, CallContext context = default);
            ValueTask<LiquidateCompanyResponce> LiquidateCompany(LiquidateCompanyRequest request, CallContext context = default);
            ValueTask<LiquidateCompanyResponce> LiquidateCompanyConfirm(LiquidateCompanyConfirmRequest request, CallContext context = default);

        }
    }
}
