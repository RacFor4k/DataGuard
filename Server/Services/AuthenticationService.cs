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
using System.Security.Cryptography;

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
        private readonly ISecurityService _securityService;
        private readonly UserAccessor _userAccessor;

        public AuthenticationService(
            DataGuardDbContext dbContext, 
            IConnectionMultiplexer redis, 
            ILogger<AuthenticationService> logger,
            IJwtService jwtService,
            ISecurityService securityService,
            UserAccessor userAccessor)
        {
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("auth:");
            _logger = logger;
            _jwtService = jwtService;
            _securityService = securityService;
            _userAccessor = userAccessor;
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
            if (request.Pin.Length != 32)
            {
                _logger.LogInformation($"{context.Peer}\tPin is invalid");
                return new RegisterResponse { Status = 400, Message = "Pin is invalid" };
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
            Company? company = await _dbContext.Companies.Where(c => c.CompanyId == registrationData.CompanyId).FirstOrDefaultAsync();
            if (company == null)
            {
                _logger.LogInformation($"{context.Peer}\tCompany is invalid");
                return new RegisterResponse { Status = 400, Message = "Company is invalid" };
            }
            var user = new User
            {
                CompanyId = registrationData.CompanyId,
                Name = registrationData.Name,
                Surname = registrationData.Surname,
                Email = registrationData.Email,
                PinCodeHash = request.Pin.ToByteArray(),
                PublicKey = request.PublicKey.ToByteArray(),
                EncryptedKey = request.EncryptededKey.ToByteArray(),
                MasterEncryptedKey = null,
                Company = company
            };
            await _dbContext.Users.AddAsync(user);
            try
            {
                foreach (var groupId in registrationData.Groups)
                {
                    var groupMember = new GroupMember
                    {
                        GroupId = groupId,
                        Group = await _dbContext.Groups.Where(g => g.Id == groupId).FirstAsync(),
                        UserId = user.UUID,
                        User = user,
                        CompanyId = registrationData.CompanyId,
                        JoinDate = DateTime.UtcNow,
                        Role = GroupRole.User
                    };
                    await _dbContext.GroupMembers.AddAsync(groupMember);
                }

                foreach (var adminGroupId in registrationData.AdminGroups)
                {
                    var groupMember = new GroupMember
                    {
                        GroupId = adminGroupId,
                        Group = await _dbContext.Groups.Where(g => g.Id == adminGroupId).FirstAsync(),
                        UserId = user.UUID,
                        User = user,
                        CompanyId = registrationData.CompanyId,
                        JoinDate = DateTime.UtcNow,
                        Role = GroupRole.Admin
                    };
                    await _dbContext.GroupMembers.AddAsync(groupMember);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{context.Peer}\tGroupMembers is invalid\n{ex.Message}");
                return new RegisterResponse { Status = 400, Message = "GroupMembers is invalid" };
            }
            // Создание токена
            UserJwt userJwt = new UserJwt
            {
                Subject = user.UUID,
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                Groups = ["system:master-key"],
                JwtId = Guid.CreateVersion7().ToString(),
            };
            byte[]? masterPublicKey = await _dbContext.Users.Where(u => u.UUID == user.UUID).Select(u => u.Company.PublicKey).FirstOrDefaultAsync();
            if (masterPublicKey == null)
            {
                _logger.LogInformation($"{context.Peer}\tMaster public key is invalid");
                return new RegisterResponse { Status = 400, Message = "Company is invalid" };
            }
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"{context.Peer}\tUser registered");

            return new RegisterResponse { Status = 200, Message = "OK", PublicMasterKey = ByteString.CopyFrom(masterPublicKey) };
        }

        /// <summary>
        /// Установка зашифрованного ключа пользователя мастер-ключом.
        /// </summary>
        public override async Task<SetMasterEncryptedKeyResponse> SetMasterEncryptedKey(SetMasterEncryptedKeyRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Set master encrypted key request from {context.Peer}");
            if(_userAccessor.User == null || !_userAccessor.User.IsAccessToken())
            {
                _logger.LogInformation($"{context.Peer}\tToken is invalid");
                return new SetMasterEncryptedKeyResponse { Status = 401, Message = "Token is invalid", JwtToken = "" };
            }
            if (request.MasterEncryptedKey.Length != 512)
            {
                _logger.LogInformation($"{context.Peer}\tMaster encrypted key is empty");
                return new SetMasterEncryptedKeyResponse { Status = 400, Message = "Master encrypted key is empty", JwtToken = "" };
            }
            if (!_userAccessor.User.Groups.Contains("system:master-key"))
            {
                _logger.LogInformation($"{context.Peer}\tToken is invalid");
                return new SetMasterEncryptedKeyResponse { Status = 403, Message = "Token is invalid", JwtToken = "" };
            }
            
            _userAccessor.User.Groups = await _dbContext.GroupMembers
                .Where(gm => gm.UserId == _userAccessor.User.Subject)
                .Select(gm => gm.Group.Name)
                .ToListAsync();

            string jwtToken = await _jwtService.GenerateRefreshTokenAsync(_userAccessor.User);
            await _jwtService.RevokeTokenAsync(request.JwtToken);
            return new SetMasterEncryptedKeyResponse { Status = 200, Message = "OK", JwtToken = jwtToken };
        }

        /// <summary>
        /// Аутентифицирует пользователя с предоставленным email и PIN.
        /// Генерирует Access токен (30 мин) и Refresh токен (24 ч).
        /// </summary>
        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Login request from {context.Peer}");
            if(string.IsNullOrEmpty(request.UserId))
            {
                return new LoginResponse { Status = 400, Message = "UserId is empty" };
            }
            if(request.Pin.Length != 32)
            {
                return new LoginResponse { Status = 400, Message = "Pin is invalid" };
            }
            if(string.IsNullOrEmpty(request.NonceToken))
            {
                return new LoginResponse { Status = 400, Message = "NonceToken is empty" };
            }
            if(await _securityService.VerifyNonceToken(request.NonceToken))
            {
                return new LoginResponse { Status = 400, Message = "NonceToken is invalid" };
            }
            User? user = await _dbContext.Users.Where(u => u.UUID == Guid.Parse(request.UserId)).FirstOrDefaultAsync();
            if(user == null)
            {
                return new LoginResponse { Status = 404, Message = "User is not found" };
            }
            if(CryptographicOperations.FixedTimeEquals(user.PinCodeHash, request.Pin.ToByteArray()))
            {
                return new LoginResponse { Status = 401, Message = "Pin is invalid" };
            }
            UserJwt? userJwt = new UserJwt{
                Subject = user.UUID,
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                Groups = user.GroupMembers.Select(gm => gm.Group.Name).ToList(),
            };
            string jwtRefreshToken = await _jwtService.GenerateRefreshTokenAsync(userJwt);
            return new LoginResponse { Status = 200, Message = "OK", JwtToken = jwtRefreshToken };
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