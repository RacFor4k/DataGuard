using Grpc.Core;
using Contracts.Protos.Auth;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using Server.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf;
using Server.Auth.Interfaces;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using StackExchange.Redis.KeyspaceIsolation;
using NanoidDotNet;
using System.Security.Cryptography;
using Common.Helpers;
using Server.Auth.Options;
using Microsoft.Extensions.Options;

namespace Server.Auth.Services
{
    /// <summary>
    /// Реализация gRPC-сервиса для аутентификации пользователей.
    /// Обрабатывает регистрацию и вход через протокол gRPC.
    /// </summary>
    public class AuthenticationService : Authentication.AuthenticationBase
    {
        private readonly DataGuardDbContext _dbContext;
        private readonly IDatabase _redis;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IJwtService _jwtService;
        private readonly ISecurityService _securityService;
        private readonly SecurityOptions _securityOptions;
        private readonly UserAccessor _userAccessor;

        public AuthenticationService(
            DataGuardDbContext dbContext,
            IConnectionMultiplexer redis,
            ILogger<AuthenticationService> logger,
            IJwtService jwtService,
            ISecurityService securityService,
            IOptions<SecurityOptions> securityOptions,
            UserAccessor userAccessor)
        {
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("auth:");
            _logger = logger;
            _jwtService = jwtService;
            _securityService = securityService;
            _securityOptions = securityOptions.Value;
            _userAccessor = userAccessor;
        }
        public AuthenticationService(
            DataGuardDbContext dbContext,
            IDatabase redis,
            ILogger<AuthenticationService> logger,
            IJwtService jwtService,
            ISecurityService securityService,
            IOptions<SecurityOptions> securityOptions,
            UserAccessor userAccessor)
        {
            _dbContext = dbContext;
            _redis = redis;
            _logger = logger;
            _jwtService = jwtService;
            _securityService = securityService;
            _securityOptions = securityOptions.Value;
            _userAccessor = userAccessor;
        }

        /// <summary>
        /// Регистрирует нового пользователя с предоставленным кодом регистрации и PIN.
        /// </summary>
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"Register called, peer: {context.Peer}");

            if (string.IsNullOrEmpty(request.RegistrationCode))
            {
                _logger.LogWarning($"Registration code is empty, peer: {context.Peer}");
                return new RegisterResponse { Status = 400, Message = "Registration code is empty" };
            }
            var registrationCode = request.RegistrationCode.ToUpper();
            if (request.EncryptedPassword.Length != _securityOptions.EncryptedPasswordLength + _securityOptions.NonceLength + _securityOptions.TagLength)
            {
                _logger.LogWarning($"Password is invalid (length: {request.EncryptedPassword.Length}, peer: {context.Peer})");
                return new RegisterResponse { Status = 400, Message = "Password is invalid" };
            }
            if (request.EncryptedKey.Length != _securityOptions.EncryptedKeyLength + _securityOptions.NonceLength + _securityOptions.TagLength)
            {
                _logger.LogWarning($"Key is invalid (length: {request.EncryptedKey.Length}, peer: {context.Peer})");
                return new RegisterResponse { Status = 400, Message = "Key is invalid" };
            }
            if (request.PasswordHash.Length != _securityOptions.PasswordHashLength + _securityOptions.SaltLength)
            {
                _logger.LogWarning($"Password hash is invalid (length: {request.PasswordHash.Length}, peer: {context.Peer})");
                return new RegisterResponse { Status = 400, Message = "Password hash is invalid" };
            }
            if (request.ClientSalt.Length != _securityOptions.SaltLength)
            {
                _logger.LogWarning($"Client salt is invalid (length: {request.ClientSalt.Length}, expected: {_securityOptions.SaltLength}, peer: {context.Peer})");
                return new RegisterResponse { Status = 400, Message = "Client salt is invalid" };
            }
            if (request.BackupEncryptedKey.Length != _securityOptions.RsaKeySize / 8)
            {
                _logger.LogWarning($"Backup encrypted key is invalid (length: {request.BackupEncryptedKey.Length}, expected: {_securityOptions.EncryptedKeyLength + _securityOptions.NonceLength + _securityOptions.TagLength}, peer: {context.Peer})");
                return new RegisterResponse { Status = 400, Message = "Backup encrypted key is invalid" };
            }
            string? rawRegistrationData = await _redis.StringGetAsync(registrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogWarning($"Registration code is invalid, peer: {context.Peer}");
                return new RegisterResponse { Status = 400, Message = "Registration code is invalid" };
            }
            RegistrationData? registrationData = JsonSerializer.Deserialize<RegistrationData>(rawRegistrationData);
            if (registrationData == null)
            {
                _logger.LogWarning($"Registration data is invalid, peer: {context.Peer}");
                return new RegisterResponse { Status = 400, Message = "Registration data is invalid" };
            }

            if (await _dbContext.Users.AnyAsync(u => u.Email == registrationData.Email))
            {
                _logger.LogWarning($"User is already registered (email: {registrationData.Email}, peer: {context.Peer})");
                return new RegisterResponse { Status = 409, Message = "User is already registered" };
            }
            Company? company = await _dbContext.Companies.Where(c => c.CompanyId == registrationData.CompanyId).FirstOrDefaultAsync();
            if (company == null)
            {
                _logger.LogWarning($"Company is invalid (companyId: {registrationData.CompanyId}, peer: {context.Peer})");
                return new RegisterResponse { Status = 400, Message = "Company is invalid" };
            }
            byte[] clientSalt = request.ClientSalt.ToByteArray();
            byte[] passwordHash = new byte[_securityOptions.PasswordHashLength];
            Buffer.BlockCopy(request.PasswordHash.ToByteArray(), _securityOptions.SaltLength, passwordHash, 0, _securityOptions.PasswordHashLength);
            var user = new User
            {
                Name = registrationData.Name,
                Surname = registrationData.Surname,
                Email = registrationData.Email,
                EncryptedPassword = request.EncryptedPassword.ToByteArray(),
                EncryptedKey = request.EncryptedKey.ToByteArray(),
                ServerPasswordHash = passwordHash,
                ClientSalt = clientSalt,
                ServerSalt = _securityService.GenerateSalt(),
                BackupEncryptedKey = request.BackupEncryptedKey.ToByteArray(),
                CompanyId = company.CompanyId,
                Company = company
            };
            _dbContext.Users.Add(user);
            try
            {
                _logger.LogTrace($"Starting to add group members for user (userId: {user.UserId}, email: {registrationData.Email}, groupCount: {registrationData.Groups.Count()}, peer: {context.Peer})");
                var groups = await _dbContext.Groups.Where(g => registrationData.Groups.Contains(g.Id)).ToListAsync();
                var groupMembers = new List<GroupMember>();
                foreach (var group in groups)
                {
                    var groupMember = new GroupMember
                    {
                        GroupId = group.Id,
                        Group = group,
                        UserId = user.UserId,
                        User = user,
                        CompanyId = registrationData.CompanyId,
                        JoinDate = DateTime.UtcNow,
                        Role = registrationData.AdminGroups.Contains(group.Id) ? GroupRole.Admin : GroupRole.User
                    };
                    groupMembers.Add(groupMember);
                }
                await _dbContext.GroupMembers.AddRangeAsync(groupMembers);
                _logger.LogTrace($"Group members added successfully (userId: {user.UserId}, groupCount: {groupMembers.Count}, peer: {context.Peer})");
                if (company.PublicKeyPem == null)
                {
                    _logger.LogWarning($"Master public key is invalid (userId: {user.UserId}, companyId: {company.CompanyId}, peer: {context.Peer})");
                    return new RegisterResponse { Status = 400, Message = "Company is invalid" };
                }
                await _dbContext.SaveChangesAsync();
                await _redis.KeyDeleteAsync(registrationCode);
                _logger.LogInformation($"User registered successfully (userId: {user.UserId}, email: {registrationData.Email}, peer: {context.Peer})");
                string refreshJwtToken = _jwtService.GenerateRefreshToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, groupMembers.Select(gm => gm.Group.Name).ToArray());
                string accessJwtToken = _jwtService.GenerateAccessToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, groupMembers.Select(gm => gm.Group.Name).ToArray());
                _logger.LogTrace($"JWT tokens generated (userId: {user.UserId}, peer: {context.Peer})");

                return new RegisterResponse { Status = 200, Message = "OK", UserId = user.UserId.ToString(), Email = registrationData.Email, CompanyPublicKeyPem = company.PublicKeyPem, JwtAccessToken = accessJwtToken, JwtRefreshToken = refreshJwtToken };
            }
            catch (Exception ex)
            {
                _logger.LogError($"GroupMembers is invalid (userId: {user.UserId}, email: {registrationData.Email}, error: {ex.Message}, peer: {context.Peer})");
                return new RegisterResponse { Status = 400, Message = "GroupMembers is invalid" };
            }
        }

        /// <summary>
        /// Аутентифицирует пользователя с предоставленным email и паролем.
        /// Генерирует Access токен (30 мин) и Refresh токен (24 ч).
        /// </summary>
        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"Login called with userId: {request.UserId}, peer: {context.Peer}");

            if (string.IsNullOrEmpty(request.UserId))
            {
                _logger.LogWarning($"UserId is empty (peer: {context.Peer})");
                return new LoginResponse { Status = 400, Message = "UserId is empty" };
            }
            if (request.PasswordHash.Length != _securityOptions.SaltLength + _securityOptions.PasswordHashLength)
            {
                _logger.LogWarning($"Password is invalid (length: {request.PasswordHash.Length}, peer: {context.Peer})");
                return new LoginResponse { Status = 400, Message = "Password is invalid" };
            }
            if (string.IsNullOrEmpty(request.NonceToken))
            {
                _logger.LogWarning($"NonceToken is empty (peer: {context.Peer})");
                return new LoginResponse { Status = 400, Message = "NonceToken is empty" };
            }
            if (!await _securityService.VerifyNonceToken(request.NonceToken))
            {
                _logger.LogWarning($"NonceToken is invalid (peer: {context.Peer})");
                return new LoginResponse { Status = 400, Message = "NonceToken is invalid" };
            }
            _logger.LogTrace($"Fetching user by userId: {request.UserId} (peer: {context.Peer})");
            if (!Guid.TryParse(request.UserId, out Guid userId))
            {
                _logger.LogWarning($"UserId is invalid (peer: {context.Peer})");
                return new LoginResponse { Status = 400, Message = "UserId is invalid" };
            }
            User? user = await _dbContext.Users.Where(u => u.UserId == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                _logger.LogWarning($"User is not found (userId: {request.UserId}, peer: {context.Peer})");
                return new LoginResponse { Status = 404, Message = "User is not found" };
            }
            _logger.LogTrace($"User found, verifying password (userId: {user.UserId}, email: {user.Email}, peer: {context.Peer})");
            byte[] clientHash = new byte[_securityOptions.PasswordHashLength];
            Buffer.BlockCopy(request.PasswordHash.ToByteArray(), _securityOptions.SaltLength, clientHash, 0, _securityOptions.PasswordHashLength);
            if (!CryptographicOperations.FixedTimeEquals(user.ServerPasswordHash, clientHash))
            {
                _logger.LogWarning($"Password is invalid (userId: {user.UserId}, email: {user.Email}, peer: {context.Peer})");
                return new LoginResponse { Status = 401, Message = "Password is invalid" };
            }
            _logger.LogTrace($"Generating JWT tokens for user (userId: {user.UserId}, name: {user.Name}, peer: {context.Peer})");
            string jwtRefreshToken = _jwtService.GenerateRefreshToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, user.GroupMembers.Select(gm => gm.Group.Name).ToArray());
            string jwtAccessToken = _jwtService.GenerateAccessToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, user.GroupMembers.Select(gm => gm.Group.Name).ToArray());
            _logger.LogInformation($"Login successful (userId: {user.UserId}, email: {user.Email}, peer: {context.Peer})");
            return new LoginResponse { Status = 200, Message = "OK", EncryptedKey = ByteString.CopyFrom(user.EncryptedKey), JwtRefreshToken = jwtRefreshToken, JwtAccessToken = jwtAccessToken };
        }

        /// <summary>
        /// Обновляет токен пользователя с предоставленным токеном.
        /// Валидирует Refresh-токен и выдает новый Access токен.
        /// Refresh токен не rotates из-за ограничений протокола гRPC.
        /// </summary>
        public override async Task<RefreshTokenResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
        {
            _logger.LogTrace($"RefreshToken called with peer: {context.Peer}");

            if (_userAccessor.userJwt == null)
            {
                _logger.LogWarning($"Token is invalid (userJwt is null, peer: {context.Peer})");
                return new RefreshTokenResponse { Status = 401, Message = "Token is invalid" };
            }
            if (!_userAccessor.userJwt.IsAccessToken())
            {
                _logger.LogTrace($"Refreshing token (userJwt type is refresh, peer: {context.Peer})");
                if (!Guid.TryParse(_userAccessor.userJwt.Subject, out Guid requestUserId))
                {
                    _logger.LogWarning($"Token is invalid (userId: {_userAccessor.userJwt.Subject}, peer: {context.Peer})");
                    return new RefreshTokenResponse { Status = 400, Message = "Token is invalid" };
                }
                User? user = _dbContext.Users
                    .Include(u => u.GroupMembers)
                    .ThenInclude(gm => gm.Group)
                    .FirstOrDefault(u => u.UserId == requestUserId);
                if (user == null)
                {
                    _logger.LogWarning($"Token is invalid (userId: {requestUserId}, peer: {context.Peer})");
                    return new RefreshTokenResponse { Status = 400, Message = "Token is invalid" };
                }
                string[] groups = user.GroupMembers.Select(gm => gm.Group.Name).ToArray();
                string jwtAccessToken = _jwtService.GenerateAccessToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, groups);
                string jwtRefreshToken = _jwtService.GenerateRefreshToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, groups);
                _logger.LogInformation($"Token refreshed successfully (userId: {_userAccessor.userJwt.Subject}, peer: {context.Peer})");
                return new RefreshTokenResponse { Status = 200, Message = "OK", JwtAccessToken = jwtAccessToken, JwtRefreshToken = jwtRefreshToken };
            }
            _logger.LogWarning($"Token is invalid (userJwt type is access, not refresh, peer: {context.Peer})");
            return new RefreshTokenResponse { Status = 401, Message = "Token is invalid" };
        }

        public override async Task<CreateRegistrationCodeResponse> CreateRegistrationCode(CreateRegistrationCodeRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Name is empty" };
            }

            if (string.IsNullOrEmpty(request.Surname))
            {
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Surname is empty" };
            }

            if (string.IsNullOrEmpty(request.Email))
            {
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Email is empty" };
            }

            if (request.Groups.Count == 0)
            {
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Groups is empty" };
            }

            if (_userAccessor.userJwt == null || !_userAccessor.userJwt.IsAccessToken())
            {
                _logger.LogWarning($"Token is invalid (userJwt is null: {_userAccessor.userJwt == null}, isAccessToken: {_userAccessor.userJwt?.IsAccessToken() ?? false}, peer: {context.Peer})");
                return new CreateRegistrationCodeResponse { Status = 401, Message = "Token is invalid" };
            }
            Guid companyId = _dbContext.Users.Where(u => u.UserId.ToString() == _userAccessor.userJwt.Subject).Select(u => u.CompanyId).FirstOrDefault();
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
                if (!await _redis.StringSetAsync(registrationCode, JsonSerializer.Serialize(registrationData)))
                {
                    _logger.LogError($"Registration code generation failed, peer: {context.Peer}");
                    return new CreateRegistrationCodeResponse { Status = 507, Message = "Registration code is invalid" };
                }
                _logger.LogInformation($"Registration code created successfully (name: {request.Name}, email: {request.Email}, peer: {context.Peer})");
                return new CreateRegistrationCodeResponse { Status = 200, Message = "OK", RegistrationCode = registrationCode };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Registration data is invalid (name: {request.Name}, email: {request.Email}, error: {ex.Message}, peer: {context.Peer})");
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Registration data is invalid" };
            }


        }
    }
}