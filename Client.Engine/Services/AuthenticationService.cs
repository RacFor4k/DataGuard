using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.Protos.Client.Auth;
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
            _logger.LogInformation($"Register request\n\tRegistration code: {request.RegistrationCode}\n\t Pin: {request.Pin}\n\t From: {context.RequestHeaders.GetValue("User-Agent")}");
            if (request.RegistrationCode.Length != 12
                || !request.RegistrationCode.All(char.IsLetterOrDigit))
            {
                return new RegisterResponse { Status = 400, Message = "Registration code is required." };
            }
            if (request.Pin.Length < 8)
                return new RegisterResponse { Status = 400, Message = "Pin too short" };
            if (request.Pin.Length > 32)
                return new RegisterResponse { Status = 400, Message = "Pin too long" };
            if (request.Pin.Any(char.IsWhiteSpace))
                return new RegisterResponse { Status = 400, Message = "Pin cannot contain whitespace" };
            if (!request.Pin.Any(char.IsUpper))
                return new RegisterResponse { Status = 400, Message = "Pin must contain at least one uppercase letter" };
            if (!request.Pin.Any(char.IsLower))
                return new RegisterResponse { Status = 400, Message = "Pin must contain at least one lowercase letter" };
            if (!request.Pin.Any(char.IsDigit))
                return new RegisterResponse { Status = 400, Message = "Pin must contain at least one digit" };
            if (!request.Pin.Any(ch => !char.IsLetterOrDigit(ch)))
                return new RegisterResponse { Status = 400, Message = "Pin must contain at least one special character" };

            
        }

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            // TODO: реализовать
            return new LoginResponse { Status = 200, Message = "OK" };
        }
    }
}