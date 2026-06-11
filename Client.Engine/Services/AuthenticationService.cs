using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Client.Engine.Helpers;
using Client.Engine.Options;
using Contracts.Protos.Client.Auth;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Client.Engine.Services
{
    public class AuthenticationService : Authentication.AuthenticationBase
    {
        private readonly ILogger<AuthenticationService> _logger;
        private readonly SecurityOptions _securityOptions;
        private readonly Contracts.Protos.Auth.Authentication.AuthenticationClient _authClient;
        public AuthenticationService(ILogger<AuthenticationService> logger, IOptions<SecurityOptions> securityOptions, Contracts.Protos.Auth.Authentication.AuthenticationClient authClient)
        {
            _logger = logger;
            _securityOptions = securityOptions.Value;
            _authClient = authClient;
        }
        
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Register request\n\tRegistration code: {request.RegistrationCode}\n\t Password: {request.Password}\n\t From: {context.RequestHeaders.GetValue("User-Agent")}");
            if (request.RegistrationCode.Length != 12
                || !request.RegistrationCode.All(char.IsLetterOrDigit))
            {
                return new RegisterResponse { Status = 400, Message = "Registration code is required." };
            }
            if (request.Password.Length < _securityOptions.Password.MinimumLength)
                return new RegisterResponse { Status = 400, Message = "Password too short" };
            if (request.Password.Length > _securityOptions.Password.MaximumLength)
                return new RegisterResponse { Status = 400, Message = "Password too long" };
            if (request.Password.Any(char.IsWhiteSpace))
                return new RegisterResponse { Status = 400, Message = "Password cannot contain whitespace" };
            if (!request.Password.Any(char.IsUpper))
                return new RegisterResponse { Status = 400, Message = "Password must contain at least one uppercase letter" };
            if (!request.Password.Any(char.IsLower))
                return new RegisterResponse { Status = 400, Message = "Password must contain at least one lowercase letter" };
            if (!request.Password.Any(char.IsDigit))
                return new RegisterResponse { Status = 400, Message = "Password must contain at least one digit" };
            if (!request.Password.Any(ch => !char.IsLetterOrDigit(ch)))
                return new RegisterResponse { Status = 400, Message = "Password must contain at least one special character" };
            byte[] key = RandomNumberGenerator.GetBytes(32);
            byte[] salt = RandomNumberGenerator.GetBytes(_securityOptions.SaltLength);
            byte[] encryptedKey = SecurityHelper.EncryptKey(request.Password, key, salt, _securityOptions.NonceLength, _securityOptions.TagLength, _securityOptions.HashIterations, _securityOptions.HashLength);
            byte[] encryptedPassword = SecurityHelper.EncryptPassword(request.Password, key, _securityOptions.NonceLength, _securityOptions.TagLength);
            byte[] passwordHash = SecurityHelper.GetSecurityHash(request.Password, salt, _securityOptions.Argon2.DegreeOfParallelism, _securityOptions.Argon2.Iterations, _securityOptions.Argon2.MemorySize, _securityOptions.HashLength);
            var registerResponse = await _authClient.RegisterAsync(new Contracts.Protos.Auth.RegisterRequest
            {
                RegistrationCode = request.RegistrationCode,
                EncryptedKey = ByteString.CopyFrom(encryptedKey),
                EncryptedPassword = ByteString.CopyFrom(encryptedPassword),
                ClientSalt = ByteString.CopyFrom(salt),
                PasswordHash = ByteString.CopyFrom(passwordHash)
            });
            if (registerResponse.Status != 200)
            {
                return new RegisterResponse { Status = registerResponse.Status, Message = registerResponse.Message };
            }
            return new RegisterResponse { Status = 200, Message = "OK" };
        }

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            // TODO: реализовать
            return new LoginResponse { Status = 200, Message = "OK" };
        }
    }
}