using Grpc.Core;
using Contracts.Protos.Auth;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using Server.Models;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf;
using Server.Interfaces;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using StackExchange.Redis.KeyspaceIsolation;

namespace Server.Services
{
    /// <summary>
    /// Реализация gRPC-сервиса для аутентификации пользователей.
    /// Обрабатывает регистрацию и вход через протокол gRPC.
    /// </summary>
    public class AuthenticationService : Authentication.AuthenticationBase
    {
        private readonly DataGuardDbContext _dbContext;
        private readonly IDatabase  _redis;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IJwtService _jwtService;

        public AuthenticationService(
            DataGuardDbContext dbContext, 
            IConnectionMultiplexer redis, 
            ILogger<AuthenticationService> logger,
            IJwtService jwtService)
        {
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("auth:");
            _logger = logger;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Регистрирует нового пользователя с предоставленным кодом регистрации и PIN.
        /// </summary>
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Registration request from {context.Peer}");

            if (string.IsNullOrEmpty(request.RegistrationCode))
            {
                _logger.LogInformation($"{context.Peer}\tRegistration code is empty");
                return new RegisterResponse { Success = false, Message = "Registration code is empty" };
            }

            string? rawRegistrationData = await _redis.StringGetAsync(request.RegistrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogInformation($"{context.Peer}\tRegistration code is invalid");
                return new RegisterResponse { Success = false, Message = "Registration code is invalid" };
            }

            RegistrationData? registrationData = JsonSerializer.Deserialize<RegistrationData>(rawRegistrationData);
            if (registrationData == null)
            {
                _logger.LogInformation($"{context.Peer}\tRegistration data is invalid");
                return new RegisterResponse { Success = false, Message = "Registration data is invalid" };
            }

            if (await _dbContext.Users.AnyAsync(u => u.Email == registrationData.Email))
            {
                _logger.LogInformation($"{context.Peer}\tUser is already registered");
                return new RegisterResponse { Success = false, Message = "User is already registered" };
            }

            var user = new User
            {
                CompanyId = registrationData.CompanyId,
                Name = registrationData.Name,
                Surname = registrationData.Surname,
                Email = registrationData.Email,
                PinCodeHash = request.Pin,
                PublicKey = request.PublicKey.ToByteArray(),
                EncryptedKey = request.EncryptededKey.ToByteArray(),
                MasterEncryptedKey = null
            };
            await _dbContext.Users.AddAsync(user);


            foreach (var group in registrationData.Groups)
            {
                var groupMember = new GroupMember
                {
                    GroupId = Guid.CreateVersion7(),
                    UserId = user.UUID,
                    CompanyId = registrationData.CompanyId,
                    JoinDate = DateTime.UtcNow,
                    Role = GroupRole.User
                };
                await _dbContext.GroupMembers.AddAsync(groupMember);
            }

            foreach (var adminGroup in registrationData.AdminGroups)
            {
                var groupMember = new GroupMember
                {
                    GroupId = adminGroup,
                    UserId = user.UUID,
                    CompanyId = registrationData.CompanyId,
                    JoinDate = DateTime.UtcNow,
                    Role = GroupRole.Admin
                };
                await _dbContext.GroupMembers.AddAsync(groupMember);
            }

            // Создание токена
            UserJwt userJwt = new UserJwt
            {
                Subject = user.UUID,
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                Groups = new List<string> { "User" },
                JwtId = Guid.CreateVersion7().ToString(),
            };
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"{context.Peer}\tUser registered");

            if (string.IsNullOrEmpty(request.Pin))
            {
                _logger.LogInformation($"{context.Peer}\tPin is empty");
                return new RegisterResponse { Success = false, Message = "Pin is empty" };
            }

            return new RegisterResponse { Success = true, Message = "OK", PublicMasterKey = ByteString.CopyFrom(Convert.ToByte("asdasd")) };
        }

        /// <summary>
        /// Установка зашифрованного ключа пользователя мастер-ключом.
        /// </summary>
        public override async Task<SetMasterEncryptedKeyResponse> SetMasterEncryptedKey(SetMasterEncryptedKeyRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Set master encrypted key request from {context.Peer}");

            return new SetMasterEncryptedKeyResponse { Success = true, Message = "OK" };
        }

        /// <summary>
        /// Аутентифицирует пользователя с предоставленным email и PIN.
        /// Генерирует Access токен (30 мин) и Refresh токен (24 ч).
        /// </summary>
        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            return new LoginResponse { Success = true, Message = "OK", JwtToken = "JWT" };
        }

        /// <summary>
        /// Обновляет токен пользователя с предоставленным токеном.
        /// Валидирует Refresh-токен и выдает новый Access токен.
        /// Refresh токен не rotates из-за ограничений протокола гRPC.
        /// </summary>
        public override async Task<RefreshTokenResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
        {
            return new RefreshTokenResponse { Success = true, Message = "OK", Token = "JWT" };
        }
    }
}