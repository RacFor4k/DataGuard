using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.Protos.Auth;
using Grpc.Core;

namespace Client.Engine.Services
{
    public class AuthenticationService : Authentication.AuthenticationBase
    {
        private readonly ILogger<AuthenticationService> _logger;
        public AuthenticationService(ILogger<AuthenticationService> logger)
        {
            _logger = logger;
        }
        
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            // TODO: реализовать
            return new RegisterResponse { Status = 200, Message = "OK" };
        }

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            // TODO: реализовать
            return new LoginResponse { Status = 200, Message = "OK" };
        }
    }
}