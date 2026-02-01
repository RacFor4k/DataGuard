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
            ValueTask<SignUpResponce> SignUp(SignUpRequest request, CallContext context = default);
        }
    }
}
