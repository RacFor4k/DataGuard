using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Server.Models;
using Server.Options;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Server.Services
{
    /// <summary>
    /// Сервис для работы с JWT токенами.
    /// Реализует генерацию, валидацию, парсинг токенов доступа и обновления.
    /// </summary>
    public class JwtService : Interfaces.IJwtService
    {
        private readonly DataGuardDbContext _dbContext;
        private readonly IDatabase  _redis;
        private readonly IOptions<JwtSettings> _jwtSettings;
        public ILogger<JwtService> _logger { get; set; }
        private static readonly JwtSecurityTokenHandler _jwtHandler = new JwtSecurityTokenHandler();

        public JwtService(DataGuardDbContext dbContext, IConnectionMultiplexer redis, IOptions<JwtSettings> jwtSettings, ILogger<JwtService> logger)
        {
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("jwt:");
            _jwtSettings = jwtSettings;
            if(string.IsNullOrWhiteSpace(_jwtSettings.Value.Issuer))
                throw new InvalidOperationException("JWT Issuer not found in appsettings.json");
            if(string.IsNullOrWhiteSpace(_jwtSettings.Value.Audience))
                throw new InvalidOperationException("JWT Audience not found in appsettings.json");
            if(string.IsNullOrWhiteSpace(_jwtSettings.Value.HexKey))
                throw new InvalidOperationException("JWT HexKey not found in appsettings.json");
            _logger = logger;
        }
        /// <summary>
        /// Генерирует Access токен.
        /// Заполянется nullable поля в UserJwt.
        /// </summary>
        /// <param name="userJwt">UserJwt объект.</param>
        /// <returns>Access токен.</returns>
        public string GenerateAccessToken(string subject, string name, string surname, string email, string[] groups) 
        {
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Convert.FromHexString(_jwtSettings.Value.HexKey));
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            
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
            claims.Concat(groups.Select(g => new Claim("role", g)));
            
            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _jwtSettings.Value.Issuer,
                audience: _jwtSettings.Value.Audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_jwtSettings.Value.AccessTokenExpiration),
                signingCredentials: credentials
            );
            return _jwtHandler.WriteToken(token);
        }
        
        public string GenerateRefreshToken(string subject, string name, string surname, string email, string[] groups)
        {
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Convert.FromHexString(_jwtSettings.Value.HexKey));
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

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
            claims.Concat(groups.Select(g => new Claim("role", g)));
            
            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _jwtSettings.Value.Issuer,
                audience: _jwtSettings.Value.Audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_jwtSettings.Value.RefreshTokenExpiration),
                signingCredentials: credentials);

            return _jwtHandler.WriteToken(token);
        }

        public async Task<JwtSecurityToken?> VerifyTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityKey = new SymmetricSecurityKey(Convert.FromHexString(_jwtSettings.Value.HexKey));

            var validationParameters = new TokenValidationParameters
            {
                // Проверка подписи
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,

                // Проверка издателя и потребителя
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Value.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Value.Audience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1) // Допустимый сдвиг времени для компенсации рассинхронизации часов
            };

            try
            {
                // Метод ValidateToken проверяет подпись, время действия (Lifetime), Issuer и Audience
                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken validatedJwtToken)
                {
                    return null;
                }

                if (await IsTokenRevokedAsync(validatedJwtToken))
                {
                    return null;
                }
                return validatedJwtToken;
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogInformation($"Token expired: {ex.Message}");
                return null;
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                _logger.LogInformation($"Invalid token signature: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Token validation failed: {ex.Message}");
                return null;
            }
        }
        public async Task<bool> RevokeTokenAsync(JwtSecurityToken jwtToken)
        {
            try
            {
                if(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == null)
                    throw new InvalidOperationException("Token type claim not found");
                if(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == "access")
                {
                    if (!DateTime.TryParse(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value, out DateTime expirationTime))
                    {
                        return true;
                    }
                    TimeSpan ttl = expirationTime - DateTime.UtcNow;
                    if (ttl < TimeSpan.Zero)
                    {
                        return true;
                    }
                    await _redis.StringSetAsync($"blacklist:{jwtToken.Id}", jwtToken.Subject.ToString(), ttl);
                    return true;
                }
                else
                {
                    UserJwt? userJwt = await _dbContext.UserJwtRefreshTokens.FindAsync(jwtToken.Id);
                    if (userJwt == null)
                    {
                        return true;
                    }
                    _dbContext.UserJwtRefreshTokens.Remove(userJwt);
                    await _dbContext.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Token revoking failed: {e.Message}");
                return false;
            }
        }        
        public async Task<bool> IsTokenRevokedAsync(JwtSecurityToken jwtToken)
        {
            if(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == null)
                throw new InvalidOperationException("Token type claim not found");
            if(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == "access")
                return await _redis.KeyExistsAsync($"blacklist:{jwtToken.Id}");
            else
            {
                UserJwt? userJwt = await _dbContext.UserJwtRefreshTokens.FindAsync(jwtToken.Id);
                if (userJwt == null)
                {
                    return false;
                }
                return true;
            }
        }
        
        public JwtSecurityToken ParseToken(string token)
        {
            return _jwtHandler.ReadJwtToken(token);
        }
    }
}