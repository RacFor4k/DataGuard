using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Client.Engine.Helpers;
using Client.Engine.Interfaces;
using Client.Engine.Models;
using Client.Engine.Options;
using Contracts.Protos.Client.Auth;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Common.Helpers;

namespace Client.Engine.Services
{
    public class AuthenticationService : Authentication.AuthenticationBase
    {
        private readonly ILogger<AuthenticationService> _logger;
        private readonly SecurityOptions _securityOptions;
        private readonly Contracts.Protos.Auth.Authentication.AuthenticationClient _authClient;
        private readonly Contracts.Protos.Security.SecurityService.SecurityServiceClient _securityServiceClient;
        private readonly IJwtTokenProvider _jwtTokenProvider;
        private readonly IKeyProvider _keyProvider;
        private readonly Contracts.Protos.CompanyManager.CompanyManager.CompanyManagerClient _companyManagerClient;
        private readonly AgentDbContext _dbContext;
        public AuthenticationService(
            ILogger<AuthenticationService> logger,
            IOptions<SecurityOptions> securityOptions,
            Contracts.Protos.Auth.Authentication.AuthenticationClient authClient,
            Contracts.Protos.Security.SecurityService.SecurityServiceClient securityServiceClient,
            IJwtTokenProvider jwtTokenProvider,
            Contracts.Protos.CompanyManager.CompanyManager.CompanyManagerClient companyManagerClient,
            AgentDbContext dbContext,
            IKeyProvider keyProvider)
        {
            _logger = logger;
            _securityOptions = securityOptions.Value;
            _authClient = authClient;
            _securityServiceClient = securityServiceClient;
            _jwtTokenProvider = jwtTokenProvider;
            _companyManagerClient = companyManagerClient;
            _dbContext = dbContext;
            _keyProvider = keyProvider;
        }        

        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Register request from: {context.RequestHeaders.GetValue("User-Agent")}");
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
            string? companyPublicKeyPem = null;
            if (!string.IsNullOrEmpty(request.CompanyPublicKeyPem))
            {
                companyPublicKeyPem = request.CompanyPublicKeyPem;
                var setCompanyPublicKeyResponse = await _companyManagerClient.SetCompanyPublicKeyAsync(new Contracts.Protos.CompanyManager.SetCompanyPublicKeyRequest
                {
                    RegistrationCode = request.RegistrationCode,
                    CompanyPublicKeyPem = companyPublicKeyPem
                });
                if (setCompanyPublicKeyResponse.Status != 200)
                {
                    _logger.LogError($"Failed to set company public key: {setCompanyPublicKeyResponse.Message}");
                    return new RegisterResponse { Status = setCompanyPublicKeyResponse.Status, Message = setCompanyPublicKeyResponse.Message };
                }
            }
            else
            {
                _logger.LogTrace($"Company public key not provided, fetching from server (peer: {context.Peer})");
                var getCompanyPublicKeyResponse = await _companyManagerClient.GetCompanyPublicKeyAsync(new Contracts.Protos.CompanyManager.GetCompanyPublicKeyRequest
                {
                    RegistrationCode = request.RegistrationCode
                });
                if (getCompanyPublicKeyResponse.Status != 200)
                {
                    _logger.LogError($"Failed to get company public key: {getCompanyPublicKeyResponse.Message}");
                    return new RegisterResponse { Status = getCompanyPublicKeyResponse.Status, Message = getCompanyPublicKeyResponse.Message };
                }
                companyPublicKeyPem = getCompanyPublicKeyResponse.CompanyPublicKeyPem;
            }
            byte[] key = RandomNumberGenerator.GetBytes(_securityOptions.KeyLength);
            byte[] salt = RandomNumberGenerator.GetBytes(_securityOptions.SaltLength);
            byte[] encryptedKey = SecurityHelper.EncryptKey(request.Password, key, salt, _securityOptions.NonceLength, _securityOptions.TagLength, _securityOptions.HashIterations, _securityOptions.HashLength);
            byte[] backupEncryptedKey = SecurityHelper.EncryptBackupKey(companyPublicKeyPem, key);
            byte[] encryptedPassword = SecurityHelper.EncryptPassword(request.Password, key, _securityOptions.NonceLength, _securityOptions.TagLength, _securityOptions.Password.EncryptedLength);
            byte[] passwordHash = SecurityHelper.GetSecurityHash(request.Password, salt, _securityOptions.Argon2.DegreeOfParallelism, _securityOptions.Argon2.Iterations, _securityOptions.Argon2.MemorySize, _securityOptions.HashLength);
            var registerResponse = await _authClient.RegisterAsync(new Contracts.Protos.Auth.RegisterRequest
            {
                RegistrationCode = request.RegistrationCode,
                EncryptedKey = ByteString.CopyFrom(encryptedKey),
                EncryptedPassword = ByteString.CopyFrom(encryptedPassword),
                ClientSalt = ByteString.CopyFrom(salt),
                PasswordHash = ByteString.CopyFrom(passwordHash),
                BackupEncryptedKey = ByteString.CopyFrom(backupEncryptedKey),
            });
            if (registerResponse.Status != 200)
            {
                return new RegisterResponse { Status = registerResponse.Status, Message = registerResponse.Message };
            }
            var setMasterKeyRequestMetadata = new Metadata
            {
                { "Authorization", $"Bearer {registerResponse.JwtAccessToken}" }
            };
            if(!Guid.TryParse(registerResponse.UserId, out Guid accountId))
            {
                _logger.LogError($"UserId is invalid (peer: {context.Peer})");
                return new RegisterResponse { Status = 500, Message = "UserId is invalid" };
            }
            var account = new Account
            {
                AccountId = accountId,
                Email = registerResponse.Email,
            };
            var jwtToken = new JwtToken
            {
                AccessToken = registerResponse.JwtAccessToken,
                RefreshToken = registerResponse.JwtRefreshToken,
                AccountId = accountId,
                Account = account
            };
            account.JwtToken = jwtToken;
            _jwtTokenProvider.SetToken(jwtToken);
            _dbContext.Accounts.Add(account);
            await _dbContext.SaveChangesAsync();
            await _keyProvider.SetKeyAsync(key);
            CryptographicOperations.ZeroMemory(key);
            return new RegisterResponse { Status = 200, Message = "OK" };
        }

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"Login called");
            if (string.IsNullOrEmpty(request.AccountId))
            {
                _logger.LogWarning($"AccountId is empty (peer: {context.Peer})");
                return new LoginResponse { Status = 400, Message = "AccountId is empty" };
            }
            if (!Guid.TryParse(request.AccountId, out Guid accountId))
            {
                _logger.LogWarning($"AccountId is invalid (peer: {context.Peer})");
                return new LoginResponse { Status = 400, Message = "AccountId is invalid" };
            }
            if (request.Password.Length < _securityOptions.Password.MinimumLength)
                return new LoginResponse { Status = 400, Message = "Password too short" };
            if (request.Password.Length > _securityOptions.Password.MaximumLength)
                return new LoginResponse { Status = 400, Message = "Password too long" };
            if (request.Password.Any(char.IsWhiteSpace))
                return new LoginResponse { Status = 400, Message = "Password cannot contain whitespace" };
            if (!request.Password.Any(char.IsUpper))
                return new LoginResponse { Status = 400, Message = "Password must contain at least one uppercase letter" };
            if (!request.Password.Any(char.IsLower))
                return new LoginResponse { Status = 400, Message = "Password must contain at least one lowercase letter" };
            if (!request.Password.Any(char.IsDigit))
                return new LoginResponse { Status = 400, Message = "Password must contain at least one digit" };
            if (!request.Password.Any(ch => !char.IsLetterOrDigit(ch)))
                return new LoginResponse { Status = 400, Message = "Password must contain at least one special character" };
            var getNonceResponse = await _securityServiceClient.GetNonceAsync(new ());
            if (getNonceResponse.Status != 200)
            {
                _logger.LogWarning($"GetNonce failed (peer: {context.Peer})");
                return new LoginResponse { Status = getNonceResponse.Status, Message = getNonceResponse.Message };
            }
            var nonceToken = getNonceResponse.NonceToken;
            var getSaltResponse = await _securityServiceClient.GetSaltAsync(new Contracts.Protos.Security.SaltRequest
            {
                UserId = request.AccountId
            });
            if (getSaltResponse.Status != 200)
            {
                _logger.LogWarning($"GetSalt failed (peer: {context.Peer})");
                return new LoginResponse { Status = getSaltResponse.Status, Message = getSaltResponse.Message };
            }
            byte[] salt = getSaltResponse.Salt.ToByteArray();
            byte[] passwordHash = SecurityHelper.GetSecurityHash(request.Password, salt, _securityOptions.Argon2.DegreeOfParallelism, _securityOptions.Argon2.Iterations, _securityOptions.Argon2.MemorySize, _securityOptions.HashLength);
            var loginResponse = await _authClient.LoginAsync(new Contracts.Protos.Auth.LoginRequest
            {
                UserId = request.AccountId,
                PasswordHash = ByteString.CopyFrom(passwordHash),
                NonceToken = nonceToken
            });
            if (loginResponse.Status != 200)
            {
                _logger.LogWarning($"Login failed (peer: {context.Peer})");
                return new LoginResponse { Status = loginResponse.Status, Message = loginResponse.Message };
            }
            if (loginResponse.EncryptedKey.Length != _securityOptions.KeyLength + _securityOptions.NonceLength + _securityOptions.TagLength)
            {
                _logger.LogWarning($"Encrypted key is invalid (length: {loginResponse.EncryptedKey.Length}, expected: {_securityOptions.KeyLength + _securityOptions.NonceLength + _securityOptions.TagLength}, peer: {context.Peer})");
                return new LoginResponse { Status = 500, Message = "Encrypted key is invalid" };
            }
            byte[] encryptedKey = loginResponse.EncryptedKey.ToByteArray();
            byte[] key = SecurityHelper.DecryptKey(encryptedKey, request.Password, salt, _securityOptions.NonceLength, _securityOptions.TagLength, _securityOptions.HashIterations, _securityOptions.HashLength);
            CryptographicOperations.ZeroMemory(encryptedKey);
            await _keyProvider.SetKeyAsync(key);
            CryptographicOperations.ZeroMemory(key);
            return new LoginResponse { Status = 200, Message = "OK" };
        }
    }
}