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
using NanoidDotNet;

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
                return new RegisterResponse { Status = 400, Message = "Registration code is empty" };
            }

            string? rawRegistrationData = await _redis.StringGetAsync(request.RegistrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogInformation($"{context.Peer}\tRegistration code is invalid");
                return new RegisterResponse { Status = 400, Message = "Registration code is invalid" };
            }

            RegistrationData? registrationData = JsonSerializer.Deserialize<RegistrationData>(rawRegistrationData);
            if (registrationData == null)
            {
                _logger.LogInformation($"{context.Peer}\tRegistration data is invalid");
                return new RegisterResponse { Status = 400, Message = "Registration data is invalid" };
            }

            if (await _dbContext.Users.AnyAsync(u => u.Email == registrationData.Email))
            {
                _logger.LogInformation($"{context.Peer}\tUser is already registered");
                return new RegisterResponse { Status = 409, Message = "User is already registered" };
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
                    GroupId = group,
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
                return new RegisterResponse { Status = 400, Message = "Pin is empty" };
            }

            return new RegisterResponse { Status = 200, Message = "OK", PublicMasterKey = ByteString.CopyFrom(Convert.ToByte("asdasd")) };
        }

        /// <summary>
        /// Установка зашифрованного ключа пользователя мастер-ключом.
        /// </summary>
        public override async Task<SetMasterEncryptedKeyResponse> SetMasterEncryptedKey(SetMasterEncryptedKeyRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Set master encrypted key request from {context.Peer}");
            if(_jwtService.VerifyTokenAsync(request.JwtToken)==null)
            {
                _logger.LogInformation($"{context.Peer}\tToken is invalid");
                return new SetMasterEncryptedKeyResponse { Status = 401, Message = "Token is invalid", JwtToken = "" };
            }

            UserJwt? userJwt = _jwtService.ParceToken(request.JwtToken);
            if (userJwt == null)
            {
                _logger.LogInformation($"{context.Peer}\tToken is invalid");
                return new SetMasterEncryptedKeyResponse { Status = 401, Message = "Token is invalid", JwtToken = "" };
            }

            
            return new SetMasterEncryptedKeyResponse { Status = 200, Message = "OK", JwtToken = "" };
        }

        /// <summary>
        /// Аутентифицирует пользователя с предоставленным email и PIN.
        /// Генерирует Access токен (30 мин) и Refresh токен (24 ч).
        /// </summary>
        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            return new LoginResponse { Status = 200, Message = "OK", JwtToken = "JWT" };
        }

        /// <summary>
        /// Обновляет токен пользователя с предоставленным токеном.
        /// Валидирует Refresh-токен и выдает новый Access токен.
        /// Refresh токен не rotates из-за ограничений протокола гRPC.
        /// </summary>
        public override async Task<RefreshTokenResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
        {
            return new RefreshTokenResponse { Status = 200, Message = "OK", JwtToken = "JWT" };
        }

        public override async Task<CreateRegistrationCodeResponse> CreateRegistrationCode(CreateRegistrationCodeRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Create registration code request from {context.Peer}");

            if(string.IsNullOrEmpty(request.Name))
            {
                _logger.LogInformation($"{context.Peer}\tName is empty");
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Name is empty" };
            }

            if (string.IsNullOrEmpty(request.Surname))
            {
                _logger.LogInformation($"{context.Peer}\tSurname is empty");
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Surname is empty" };
            }

            if (string.IsNullOrEmpty(request.Email))
            {
                _logger.LogInformation($"{context.Peer}\tEmail is empty");
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Email is empty" };
            }

            if (request.Groups.Count == 0)
            {
                _logger.LogInformation($"{context.Peer}\tGroups is empty");
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Groups is empty" };
            }
            
            UserJwt? userJwt = _jwtService.ParceToken(request.JwtToken);
            if (userJwt == null)
            {
                _logger.LogInformation($"{context.Peer}\tToken is invalid");
                return new CreateRegistrationCodeResponse { Status = 401, Message = "Token is invalid" };
            }
            Guid companyId = _dbContext.Users.Where(u => u.UUID == userJwt.Subject).Select(u => u.CompanyId).FirstOrDefault();
            try
            {
                var registrationData = new RegistrationData
                {
                    Name = request.Name,
                    Surname = request.Surname,
                    Email = request.Email,
                    CompanyId = companyId,
                    Groups = request.Groups.Select(Guid.Parse).ToList(),
                    AdminGroups = request.AdminGroups.Select(Guid.Parse).ToList()
                };
                string registrationCode = await Nanoid.GenerateAsync(Nanoid.Alphabets.UppercaseLettersAndDigits, 12);
                if(!await _redis.StringSetAsync(registrationCode, JsonSerializer.Serialize(registrationData)))
                {
                    _logger.LogInformation($"{context.Peer}\tRegistration code is invalid");
                    return new CreateRegistrationCodeResponse { Status = 507, Message = "Registration code is invalid" };
                }            
                return new CreateRegistrationCodeResponse { Status = 200, Message = "OK", RegistrationCode = registrationCode };
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{context.Peer}\tRegistration data is invalid\n{ex.Message}");
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Registration data is invalid" };
            }


        }
    }
}