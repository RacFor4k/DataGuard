using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Common.Server.Models;
using Server.Auth.Options;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Server.Auth.Services
{
    /// <summary>
    /// Сервис для работы с JWT токенами.
    /// Реализует генерацию, валидацию, парсинг токенов доступа и обновления.
    /// </summary>
    public class JwtService : Interfaces.IJwtService
    {
        private readonly DataGuardDbContext _dbContext;
        private readonly IDatabase _redis;
        private readonly IOptions<JwtOptions> _jwtOptions;
        private readonly ILogger<JwtService> _logger;
        private static readonly JwtSecurityTokenHandler _jwtHandler = new JwtSecurityTokenHandler();

        // Кэширование криптографических объектов (С15)
        private static SymmetricSecurityKey? _cachedSecurityKey;
        private static SigningCredentials? _cachedCredentials;
        private static byte[]? _cachedKey;

        public JwtService(DataGuardDbContext dbContext, IConnectionMultiplexer redis, IOptions<JwtOptions> jwtOptions, ILogger<JwtService> logger)
        {
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("jwt:");
            _jwtOptions = jwtOptions;
            _logger = logger;
            if (string.IsNullOrWhiteSpace(_jwtOptions.Value.Issuer))
                throw new InvalidOperationException("JWT Issuer not found in appsettings.json");
            if (string.IsNullOrWhiteSpace(_jwtOptions.Value.Audience))
                throw new InvalidOperationException("JWT Audience not found in appsettings.json");
            if (_jwtOptions.Value.Key == null || _jwtOptions.Value.Key.Length == 0)
                throw new InvalidOperationException("JWT Key not found in appsettings.json");

            // Инициализация кэшированных криптографических объектов
            if (_cachedKey == null || !_cachedKey.SequenceEqual(_jwtOptions.Value.Key))
            {
                _cachedKey = _jwtOptions.Value.Key;
                _cachedSecurityKey = new SymmetricSecurityKey(_cachedKey);
                _cachedCredentials = new SigningCredentials(_cachedSecurityKey, SecurityAlgorithms.HmacSha256);
            }
        }

        /// <summary>
        /// Генерирует Access токен.
        /// </summary>
        /// <param name="subject">Идентификатор пользователя.</param>
        /// <param name="name">Имя.</param>
        /// <param name="surname">Фамилия.</param>
        /// <param name="email">Email.</param>
        /// <param name="groups">Группы.</param>
        /// <returns>Access токен.</returns>
        public string GenerateAccessToken(string subject, string name, string surname, string email, string[] groups)
        {
            Claim[] claims =
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Name, $"{name} {surname}"),
                new Claim(JwtRegisteredClaimNames.GivenName, name),
                new Claim(JwtRegisteredClaimNames.FamilyName, surname),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Typ, "access"),
            };
            claims = claims.Concat(groups.Select(g => new Claim("role", g))).ToArray();

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _jwtOptions.Value.Issuer,
                audience: _jwtOptions.Value.Audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_jwtOptions.Value.AccessTokenExpiration),
                signingCredentials: _cachedCredentials!
            );
            return _jwtHandler.WriteToken(token);
        }

        public string GenerateRefreshToken(string subject, string name, string surname, string email, string[] groups)
        {
            Claim[] claims =
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Name, $"{name} {surname}"),
                new Claim(JwtRegisteredClaimNames.GivenName, name),
                new Claim(JwtRegisteredClaimNames.FamilyName, surname),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Typ, "refresh"),
            };
            claims = claims.Concat(groups.Select(g => new Claim("role", g))).ToArray();

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _jwtOptions.Value.Issuer,
                audience: _jwtOptions.Value.Audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_jwtOptions.Value.RefreshTokenExpiration),
                signingCredentials: _cachedCredentials!);

            return _jwtHandler.WriteToken(token);
        }

        public async Task<JwtSecurityToken?> VerifyTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("VerifyTokenAsync: токен пуст");
                return null;
            }

            var securityKey = _cachedSecurityKey!;

            var validationParameters = new TokenValidationParameters
            {
                // Проверка подписи
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,

                // Проверка издателя и потребителя
                ValidateIssuer = true,
                ValidIssuer = _jwtOptions.Value.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtOptions.Value.Audience,

                ValidateLifetime = true,
                // S5: Настраиваемый ClockSkew
                ClockSkew = _jwtOptions.Value.ClockSkew
            };

            try
            {
                var principal = _jwtHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken validatedJwtToken)
                {
                    _logger.LogWarning("VerifyTokenAsync: токен не является JwtSecurityToken");
                    return null;
                }

                if (await IsTokenRevokedAsync(validatedJwtToken))
                {
                    _logger.LogWarning("VerifyTokenAsync: токен отозван");
                    return null;
                }
                _logger.LogInformation("Токен успешно верифицирован");
                return validatedJwtToken;
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogWarning("VerifyTokenAsync: токен просрочен, ошибка: {Error}", ex.Message);
                return null;
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                _logger.LogWarning("VerifyTokenAsync: невалидная подпись токена, ошибка: {Error}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("VerifyTokenAsync: ошибка валидации токена, ошибка: {Error}", ex.Message);
                return null;
            }
        }

        public async Task<bool> RevokeTokenAsync(JwtSecurityToken jwtToken)
        {
            try
            {
                if (jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == null)
                {
                    _logger.LogError("RevokeTokenAsync: claim типа токена не найден");
                    throw new InvalidOperationException("Token type claim not found");
                }
                if (jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == "access")
                {
                    TimeSpan ttl = jwtToken.ValidTo - DateTime.UtcNow;
                    if (ttl < TimeSpan.Zero)
                    {
                        return true;
                    }
                    await _redis.StringSetAsync($"blacklist:{jwtToken.Id}", jwtToken.Subject.ToString(), ttl);
                    _logger.LogInformation("Access-токен успешно отозван");
                    return true;
                }
                else
                {
                    UserJwt? userJwt = await _dbContext.UserJwtRefreshTokens.FindAsync(jwtToken.Id);
                    if (userJwt == null)
                    {
                        _logger.LogWarning("RevokeTokenAsync: refresh-токен не найден в БД");
                        return true;
                    }
                    _dbContext.UserJwtRefreshTokens.Remove(userJwt);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Refresh-токен успешно отозван");
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "RevokeTokenAsync: ошибка при отзыве токена");
                return false;
            }
        }

        public async Task<bool> IsTokenRevokedAsync(JwtSecurityToken jwtToken)
        {
            if (jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == null)
            {
                _logger.LogError("IsTokenRevokedAsync: claim типа токена не найден");
                throw new InvalidOperationException("Token type claim not found");
            }
            if (jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == "access")
            {
                bool isRevoked = await _redis.KeyExistsAsync($"blacklist:{jwtToken.Id}");
                return isRevoked;
            }
            else
            {
                UserJwt? userJwt = await _dbContext.UserJwtRefreshTokens.FindAsync(jwtToken.Id);
                bool isRevoked = userJwt == null;
                return isRevoked;
            }
        }

        public JwtSecurityToken ParseToken(string token)
        {
            return _jwtHandler.ReadJwtToken(token);
        }
    }
}