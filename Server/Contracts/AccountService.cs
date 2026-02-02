using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using Server.Models.gRPC.Account;

namespace Server.Services
{
    public class AccountService
    {
        [Service]
        public interface IAccountServise
        {
            ValueTask<SignUpResponce> signUp(SignUpRequest request, CallContext context = default);
            ValueTask<SignInResponce> signIn(SignInRequest request, CallContext context = default);

            ValueTask<CreateCompanyResponce> createCompany(CreateCompanyRequest request, CallContext context = default);
            ValueTask<LiquidateCompanyResponce> liquidateCompany(LiquidateCompanyRequest request, CallContext context = default);

        }
    }
}
