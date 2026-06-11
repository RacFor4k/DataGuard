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
using Common.Helpers;

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
            var registrationCode = request.RegistrationCode.ToUpper();
            if (request.EncryptedPin.Length != 32)
            {
                _logger.LogInformation($"{context.Peer}\tPin is invalid");
                return new RegisterResponse { Status = 400, Message = "Pin is invalid" };
            }
            if (request.EncryptedKey.Length != 32)
            {
                _logger.LogInformation($"{context.Peer}\tKey is invalid");
                return new RegisterResponse { Status = 400, Message = "Key is invalid" };
            }
            if (request.PinHash.Length != 32)
            {
                _logger.LogInformation($"{context.Peer}\tPin hash is invalid");
                return new RegisterResponse { Status = 400, Message = "Pin hash is invalid" };
            }
            string? rawRegistrationData = await _redis.StringGetAsync(registrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogInformation($"{context.Peer}\tRegistration code is invalid");
                return new RegisterResponse { Status = 400, Message = "Registration code is invalid" };
            }
            await _redis.KeyDeleteAsync(registrationCode);

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
            byte[] clientSalt = _securityService.GenerateSalt();
            byte[] serverSalt = _securityService.GenerateSalt();
            byte[] serverPinHash = await _securityService.HashPasswordAsync(request.EncryptedPin.ToByteArray(), serverSalt);
            var user = new User
            {
                Name = registrationData.Name,
                Surname = registrationData.Surname,
                Email = registrationData.Email,
                EncryptedPin = request.EncryptedPin.ToByteArray(),
                EncryptedKey = request.EncryptedKey.ToByteArray(),
                ServerPinHash = serverPinHash,
                ClientSalt = clientSalt,
                ServerSalt = serverSalt,
                MasterKey = null,
                Company = company
            };
            await _dbContext.Users.AddAsync(user);
            try
            {
                var groups = _dbContext.Groups.Where(g => registrationData.Groups.Contains(g.Id));
                var groupMembers = new List<GroupMember>();
                foreach (var group in groups)
                {
                    var groupMember = new GroupMember
                    {
                        GroupId = group.Id,
                        Group = group,
                        UserId = user.UUID,
                        User = user,
                        CompanyId = registrationData.CompanyId,
                        JoinDate = DateTime.UtcNow,
                        Role = registrationData.AdminGroups.Contains(group.Id) ? GroupRole.Admin : GroupRole.User
                    };
                    groupMembers.Add(groupMember);
                }
                await _dbContext.GroupMembers.AddRangeAsync(groupMembers);
                string refreshJwtToken = _jwtService.GenerateRefreshToken(user.UUID.ToString(), user.Name, user.Surname, user.Email, user.GroupMembers.Select(gm => gm.Group.Name).ToArray());
                string accessJwtToken = _jwtService.GenerateAccessToken(user.UUID.ToString(), user.Name, user.Surname, user.Email, user.GroupMembers.Select(gm => gm.Group.Name).ToArray());
                byte[]? masterPublicKey = await _dbContext.Users.Where(u => u.UUID == user.UUID).Select(u => u.Company.PublicKey).FirstOrDefaultAsync();
                if (masterPublicKey == null)
                {
                    _logger.LogInformation($"{context.Peer}\tMaster public key is invalid");
                    return new RegisterResponse { Status = 400, Message = "Company is invalid", PublicMasterKey = ByteString.CopyFrom() };
                }
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"{context.Peer}\tUser registered");

                return new RegisterResponse { Status = 200, Message = "OK", PublicMasterKey = ByteString.CopyFrom(masterPublicKey), JwtAccessToken = accessJwtToken, JwtRefreshToken = refreshJwtToken };
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{context.Peer}\tGroupMembers is invalid\n{ex.Message}");
                return new RegisterResponse { Status = 400, Message = "GroupMembers is invalid" };
            }
        }

        /// <summary>
        /// Установка зашифрованного ключа пользователя мастер-ключом.
        /// </summary>
        public override async Task<SetMasterKeyResponse> SetMasterKey(SetMasterKeyRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Set master key request from {context.Peer}");
            if(_userAccessor.userJwt == null || !_userAccessor.userJwt.IsAccessToken())
            {
                _logger.LogInformation($"{context.Peer}\tToken is invalid");
                return new SetMasterKeyResponse { Status = 401, Message = "Token is invalid" };
            }
            if (request.MasterKey.Length != 512)
            {
                _logger.LogInformation($"{context.Peer}\tMaster key is empty");
                return new SetMasterKeyResponse { Status = 400, Message = "Master key is empty" };
            }
            if (!_userAccessor.userJwt.GetGroups().Contains("system:master-key"))
            {
                _logger.LogInformation($"{context.Peer}\tToken is invalid");
                return new SetMasterKeyResponse { Status = 403, Message = "Token is invalid" };
            }
            
            string jwtRefreshToken = _jwtService.GenerateRefreshToken(_userAccessor.userJwt.Subject, _userAccessor.userJwt.GetName(), _userAccessor.userJwt.GetSurname(), _userAccessor.userJwt.GetEmail(), _userAccessor.userJwt.GetGroups().ToArray());
            string jwtAccessToken = _jwtService.GenerateAccessToken(_userAccessor.userJwt.Subject, _userAccessor.userJwt.GetName(), _userAccessor.userJwt.GetSurname(), _userAccessor.userJwt.GetEmail(), _userAccessor.userJwt.GetGroups().ToArray());
            await _jwtService.RevokeTokenAsync(_userAccessor.userJwt);
            return new SetMasterKeyResponse { Status = 200, Message = "OK", JwtRefreshToken = jwtRefreshToken, JwtAccessToken = jwtAccessToken };
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
            if(request.PinHash.Length != 32)
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
            var pinHash = await _securityService.HashPasswordAsync(request.PinHash.ToByteArray(), user.ServerSalt);
            if(!CryptographicOperations.FixedTimeEquals(user.ServerPinHash, pinHash))
            {
                return new LoginResponse { Status = 401, Message = "Pin is invalid" };
            }
            string jwtRefreshToken = _jwtService.GenerateRefreshToken(user.UUID.ToString(), user.Name, user.Surname, user.Email, user.GroupMembers.Select(gm => gm.Group.Name).ToArray());
            string jwtAccessToken = _jwtService.GenerateAccessToken(user.UUID.ToString(), user.Name, user.Surname, user.Email, user.GroupMembers.Select(gm => gm.Group.Name).ToArray());
            return new LoginResponse { Status = 200, Message = "OK", EncryptedKey = ByteString.CopyFrom(user.EncryptedKey), JwtRefreshToken = jwtRefreshToken, JwtAccessToken = jwtAccessToken };
        }

        /// <summary>
        /// Обновляет токен пользователя с предоставленным токеном.
        /// Валидирует Refresh-токен и выдает новый Access токен.
        /// Refresh токен не rotates из-за ограничений протокола гRPC.
        /// </summary>
        public override async Task<RefreshTokenResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
        {
            if(_userAccessor.userJwt == null)
            {
                return new RefreshTokenResponse { Status = 401, Message = "Token is invalid" };
            }
            if (!_userAccessor.userJwt.IsAccessToken())
            {
                string jwtAccessToken = _jwtService.GenerateAccessToken(_userAccessor.userJwt.Subject, _userAccessor.userJwt.GetName(), _userAccessor.userJwt.GetSurname(), _userAccessor.userJwt.GetEmail(), _userAccessor.userJwt.GetGroups().ToArray());
                return new RefreshTokenResponse { Status = 200, Message = "OK", JwtAccessToken = jwtAccessToken };
            }
            return new RefreshTokenResponse { Status = 401, Message = "Token is invalid" };
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
            
            if (_userAccessor.userJwt == null || !_userAccessor.userJwt.IsAccessToken())
            {
                _logger.LogInformation($"{context.Peer}\tToken is invalid");
                return new CreateRegistrationCodeResponse { Status = 401, Message = "Token is invalid" };
            }
            Guid companyId = _dbContext.Users.Where(u => u.UUID.ToString() == _userAccessor.userJwt.Subject).Select(u => u.Company.CompanyId).FirstOrDefault();
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