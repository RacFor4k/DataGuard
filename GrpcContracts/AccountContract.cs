using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using GrpcContracts.Account;
using GrpcContracts.Company;

namespace GrpcContracts
{
    [Service]
    public interface IAccountServise
    {
        ValueTask<AuthNonceResponce> AuthNonce(AuthNonceRequest request, CallContext context = default);
        ValueTask<SignUpResponce> SignUp(SignUpRequest request, CallContext context = default);
        ValueTask<SignInResponce> SignIn(SignInRequest request, CallContext context = default);
        ValueTask<RefreshTokenResponce> RefreshToken(RefreshTokenRequest request, CallContext context = default);

        //ValueTask<CreateCompanyResponce> CreateCompany(CreateCompanyRequest request, CallContext context = default);
        //ValueTask<LiquidateCompanyResponce> LiquidateCompany(LiquidateCompanyRequest request, CallContext context = default);
        //ValueTask<LiquidateCompanyResponce> LiquidateCompanyConfirm(LiquidateCompanyConfirmRequest request, CallContext context = default);
    }
}
