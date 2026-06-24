using Grpc.Core;
using Contracts.Protos.Auth;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using Common.Server.Models;
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
using System.Net.Mail;

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

        /// <summary>
        /// Регистрирует нового пользователя с предоставленным кодом регистрации и PIN.
        /// </summary>
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.RegistrationCode))
            {
                _logger.LogWarning("Пустой код регистрации, peer: {Peer}", context.Peer);
                return new RegisterResponse { Status = 400, Message = "Registration code is empty" };
            }
            var registrationCode = request.RegistrationCode.ToUpper();
            if (request.EncryptedPassword.Length != _securityOptions.EncryptedPasswordLength + _securityOptions.NonceLength + _securityOptions.TagLength)
            {
                _logger.LogWarning("Некорректная длина пароля: {Length}, peer: {Peer}", request.EncryptedPassword.Length, context.Peer);
                return new RegisterResponse { Status = 400, Message = "Password is invalid" };
            }
            if (request.EncryptedKey.Length != _securityOptions.EncryptedKeyLength + _securityOptions.NonceLength + _securityOptions.TagLength)
            {
                _logger.LogWarning("Некорректная длина ключа: {Length}, peer: {Peer}", request.EncryptedKey.Length, context.Peer);
                return new RegisterResponse { Status = 400, Message = "Key is invalid" };
            }
            if (request.PasswordHash.Length != _securityOptions.PasswordHashLength + _securityOptions.SaltLength)
            {
                _logger.LogWarning("Некорректная длина хеша пароля: {Length}, peer: {Peer}", request.PasswordHash.Length, context.Peer);
                return new RegisterResponse { Status = 400, Message = "Password hash is invalid" };
            }
            if (request.ClientSalt.Length != _securityOptions.SaltLength)
            {
                _logger.LogWarning("Некорректная длина client salt: {Length}, ожидается: {Expected}, peer: {Peer}", request.ClientSalt.Length, _securityOptions.SaltLength, context.Peer);
                return new RegisterResponse { Status = 400, Message = "Client salt is invalid" };
            }
            if (request.BackupEncryptedKey.Length != _securityOptions.RsaKeySize / 8)
            {
                _logger.LogWarning("Некорректная длина backup encrypted key: {Length}, peer: {Peer}", request.BackupEncryptedKey.Length, context.Peer);
                return new RegisterResponse { Status = 400, Message = "Backup encrypted key is invalid" };
            }
            string? rawRegistrationData = await _redis.StringGetAsync(registrationCode);
            if (string.IsNullOrEmpty(rawRegistrationData))
            {
                _logger.LogWarning("Невалидный код регистрации, peer: {Peer}", context.Peer);
                return new RegisterResponse { Status = 400, Message = "Registration code is invalid" };
            }
            RegistrationData? registrationData = JsonSerializer.Deserialize<RegistrationData>(rawRegistrationData);
            if (registrationData == null)
            {
                _logger.LogWarning("Невалидные данные регистрации, peer: {Peer}", context.Peer);
                return new RegisterResponse { Status = 400, Message = "Registration data is invalid" };
            }

            if (await _dbContext.Users.AnyAsync(u => u.Email == registrationData.Email))
            {
                _logger.LogWarning("Пользователь уже зарегистрирован, peer: {Peer}", context.Peer);
                return new RegisterResponse { Status = 409, Message = "User is already registered" };
            }
            Company? company = await _dbContext.Companies.Where(c => c.CompanyId == registrationData.CompanyId).FirstOrDefaultAsync();
            if (company == null)
            {
                _logger.LogWarning("Невалидная компания, peer: {Peer}", context.Peer);
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
                // C4: Фильтрация групп по CompanyId
                var groups = await _dbContext.Groups.Where(g => registrationData.Groups.Contains(g.Id) && g.CompanyId == registrationData.CompanyId).ToListAsync();
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
                if (company.PublicKeyPem == null)
                {
                    _logger.LogWarning("Отсутствует мастер-публичный ключ компании, peer: {Peer}", context.Peer);
                    return new RegisterResponse { Status = 400, Message = "Company is invalid" };
                }
                await _dbContext.SaveChangesAsync();
                await _redis.KeyDeleteAsync(registrationCode);
                _logger.LogInformation("Пользователь успешно зарегистрирован, peer: {Peer}", context.Peer);
                string refreshJwtToken = _jwtService.GenerateRefreshToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, groupMembers.Select(gm => gm.Group.Name).ToArray());
                string accessJwtToken = _jwtService.GenerateAccessToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, groupMembers.Select(gm => gm.Group.Name).ToArray());

                return new RegisterResponse { Status = 200, Message = "OK", UserId = user.UserId.ToString(), Email = registrationData.Email, CompanyPublicKeyPem = company.PublicKeyPem, JwtAccessToken = accessJwtToken, JwtRefreshToken = refreshJwtToken };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении членов группы, peer: {Peer}", context.Peer);
                return new RegisterResponse { Status = 400, Message = "GroupMembers is invalid" };
            }
        }

        /// <summary>
        /// Аутентифицирует пользователя с предоставленным email и паролем.
        /// Генерирует Access токен (30 мин) и Refresh токен (24 ч).
        /// </summary>
        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.UserId))
            {
                _logger.LogWarning("Пустой UserId, peer: {Peer}", context.Peer);
                return new LoginResponse { Status = 400, Message = "UserId is empty" };
            }
            if (request.PasswordHash.Length != _securityOptions.SaltLength + _securityOptions.PasswordHashLength)
            {
                _logger.LogWarning("Некорректная длина пароля: {Length}, peer: {Peer}", request.PasswordHash.Length, context.Peer);
                return new LoginResponse { Status = 400, Message = "Password is invalid" };
            }
            if (string.IsNullOrEmpty(request.NonceToken))
            {
                _logger.LogWarning("Пустой NonceToken, peer: {Peer}", context.Peer);
                return new LoginResponse { Status = 400, Message = "NonceToken is empty" };
            }
            if (!await _securityService.VerifyNonceToken(request.NonceToken))
            {
                _logger.LogWarning("Невалидный NonceToken, peer: {Peer}", context.Peer);
                return new LoginResponse { Status = 400, Message = "NonceToken is invalid" };
            }
            if (!Guid.TryParse(request.UserId, out Guid userId))
            {
                _logger.LogWarning("Невалидный UserId, peer: {Peer}", context.Peer);
                return new LoginResponse { Status = 400, Message = "UserId is invalid" };
            }

            // H3: Жадная загрузка GroupMembers и Group
            User? user = await _dbContext.Users.Include(u => u.GroupMembers).ThenInclude(gm => gm.Group).Where(u => u.UserId == userId).FirstOrDefaultAsync();

            // C6: Защита от timing-атак — фиктивный хеш при отсутствии пользователя
            if (user == null)
            {
                // Выполнение фиктивного Argon2id для предотвращения timing-атак
                _ = await _securityService.HashPasswordAsync(Guid.NewGuid().ToByteArray(), RandomNumberGenerator.GetBytes(_securityOptions.SaltLength));
                _logger.LogWarning("Неудачная попытка входа (неверные учётные данные), peer: {Peer}", context.Peer);
                return new LoginResponse { Status = 401, Message = "Неверные учётные данные" };
            }

            byte[] clientHash = new byte[_securityOptions.PasswordHashLength];
            Buffer.BlockCopy(request.PasswordHash.ToByteArray(), _securityOptions.SaltLength, clientHash, 0, _securityOptions.PasswordHashLength);
            if (!CryptographicOperations.FixedTimeEquals(user.ServerPasswordHash, clientHash))
            {
                _logger.LogWarning("Неудачная попытка входа (неверные учётные данные), peer: {Peer}", context.Peer);
                return new LoginResponse { Status = 401, Message = "Неверные учётные данные" };
            }
            string jwtRefreshToken = _jwtService.GenerateRefreshToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, user.GroupMembers.Select(gm => gm.Group.Name).ToArray());
            string jwtAccessToken = _jwtService.GenerateAccessToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, user.GroupMembers.Select(gm => gm.Group.Name).ToArray());
            _logger.LogInformation("Успешный вход в систему, peer: {Peer}", context.Peer);
            return new LoginResponse { Status = 200, Message = "OK", EncryptedKey = ByteString.CopyFrom(user.EncryptedKey), JwtRefreshToken = jwtRefreshToken, JwtAccessToken = jwtAccessToken };
        }

        /// <summary>
        /// Обновляет токен пользователя с предоставленным токеном.
        /// Валидирует Refresh-токен и выдает новый Access токен.
        /// Refresh токен не rotates из-за ограничений протокола гRPC.
        /// </summary>
        public override async Task<RefreshTokenResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
        {
            if (_userAccessor.UserJwt == null)
            {
                _logger.LogWarning("Токен невалиден (userJwt равен null), peer: {Peer}", context.Peer);
                return new RefreshTokenResponse { Status = 401, Message = "Token is invalid" };
            }
            if (!_userAccessor.UserJwt.IsAccessToken())
            {
                if (!Guid.TryParse(_userAccessor.UserJwt.Subject, out Guid requestUserId))
                {
                    _logger.LogWarning("Невалидный subject в токене, peer: {Peer}", context.Peer);
                    return new RefreshTokenResponse { Status = 400, Message = "Token is invalid" };
                }
                // H11: Асинхронный вызов к БД
                User? user = await _dbContext.Users
                    .Include(u => u.GroupMembers)
                    .ThenInclude(gm => gm.Group)
                    .FirstOrDefaultAsync(u => u.UserId == requestUserId);
                if (user == null)
                {
                    _logger.LogWarning("Пользователь не найден при обновлении токена, peer: {Peer}", context.Peer);
                    return new RefreshTokenResponse { Status = 400, Message = "Token is invalid" };
                }
                string[] groups = user.GroupMembers.Select(gm => gm.Group.Name).ToArray();
                string jwtAccessToken = _jwtService.GenerateAccessToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, groups);
                string jwtRefreshToken = _jwtService.GenerateRefreshToken(user.UserId.ToString(), user.Name, user.Surname, user.Email, groups);
                _logger.LogInformation("Токен успешно обновлён, peer: {Peer}", context.Peer);
                return new RefreshTokenResponse { Status = 200, Message = "OK", JwtAccessToken = jwtAccessToken, JwtRefreshToken = jwtRefreshToken };
            }
            _logger.LogWarning("Невалидный тип токена для обновления, peer: {Peer}", context.Peer);
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

            // H6: Валидация формата email
            if (!MailAddress.TryCreate(request.Email, out _))
            {
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Email is invalid" };
            }

            if (request.Groups.Count == 0)
            {
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Groups is empty" };
            }

            if (_userAccessor.UserJwt == null || !_userAccessor.UserJwt.IsAccessToken())
            {
                _logger.LogWarning("Токен невалиден при создании кода регистрации, peer: {Peer}", context.Peer);
                return new CreateRegistrationCodeResponse { Status = 401, Message = "Token is invalid" };
            }

            // C2: Проверка наличия роли system:owner
            var userRoles = context.GetHttpContext().User.Claims
                .Where(c => c.Type == "role")
                .Select(c => c.Value);
            if (!userRoles.Contains("system:owner"))
            {
                _logger.LogWarning("Недостаточно прав для создания кода регистрации, peer: {Peer}", context.Peer);
                return new CreateRegistrationCodeResponse { Status = 403, Message = "Недостаточно прав для выполнения операции" };
            }

            // H11: Асинхронный вызов к БД
            Guid companyId = await _dbContext.Users.Where(u => u.UserId.ToString() == _userAccessor.UserJwt.Subject).Select(u => u.CompanyId).FirstOrDefaultAsync();

            // H2: Проверка, что AdminGroups являются подмножеством Groups
            var adminGroupIds = request.AdminGroups.Select(Guid.Parse).ToList();
            var groupIds = request.Groups.Select(Guid.Parse).ToList();
            if (!adminGroupIds.All(ag => groupIds.Contains(ag)))
            {
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Административные группы должны быть подмножеством основных групп" };
            }

            // Удаление дубликатов из списка групп
            groupIds = groupIds.Distinct().ToList();

            try
            {
                var registrationData = new RegistrationData
                {
                    Name = request.Name,
                    Surname = request.Surname,
                    Email = request.Email,
                    CompanyId = companyId,
                    Groups = groupIds,
                    AdminGroups = adminGroupIds
                };
                string registrationCode = await Nanoid.GenerateAsync(Nanoid.Alphabets.UppercaseLettersAndDigits, 12);
                // C1: TTL для кода регистрации — 4 дня
                if (!await _redis.StringSetAsync(registrationCode, JsonSerializer.Serialize(registrationData), TimeSpan.FromDays(4)))
                {
                    _logger.LogError("Ошибка генерации кода регистрации, peer: {Peer}", context.Peer);
                    return new CreateRegistrationCodeResponse { Status = 507, Message = "Registration code is invalid" };
                }
                _logger.LogInformation("Код регистрации создан успешно, peer: {Peer}", context.Peer);
                return new CreateRegistrationCodeResponse { Status = 200, Message = "OK", RegistrationCode = registrationCode };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании данных регистрации, peer: {Peer}", context.Peer);
                return new CreateRegistrationCodeResponse { Status = 400, Message = "Registration data is invalid" };
            }


        }
    }
}