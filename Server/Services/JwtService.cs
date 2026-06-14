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
        private readonly IOptions<JwtOptions> _jwtOptions;
        public ILogger<JwtService> _logger { get; set; }
        private static readonly JwtSecurityTokenHandler _jwtHandler = new JwtSecurityTokenHandler();

        public JwtService(DataGuardDbContext dbContext, IConnectionMultiplexer redis, IOptions<JwtOptions> jwtOptions, ILogger<JwtService> logger)
        {
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("jwt:");
            _jwtOptions = jwtOptions;
            if(string.IsNullOrWhiteSpace(_jwtOptions.Value.Issuer))
                throw new InvalidOperationException("JWT Issuer not found in appsettings.json");
            if(string.IsNullOrWhiteSpace(_jwtOptions.Value.Audience))
                throw new InvalidOperationException("JWT Audience not found in appsettings.json");
            if(_jwtOptions.Value.Key == null || _jwtOptions.Value.Key.Length == 0)
                throw new InvalidOperationException("JWT Key not found in appsettings.json");
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
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(_jwtOptions.Value.Key);
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
            claims = claims.Concat(groups.Select(g => new Claim("role", g))).ToArray();

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _jwtOptions.Value.Issuer,
                audience: _jwtOptions.Value.Audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_jwtOptions.Value.AccessTokenExpiration),
                signingCredentials: credentials
            );
            return _jwtHandler.WriteToken(token);
        }
        
        public string GenerateRefreshToken(string subject, string name, string surname, string email, string[] groups)
        {
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(_jwtOptions.Value.Key);
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
            claims = claims.Concat(groups.Select(g => new Claim("role", g))).ToArray();

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _jwtOptions.Value.Issuer,
                audience: _jwtOptions.Value.Audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_jwtOptions.Value.RefreshTokenExpiration),
                signingCredentials: credentials);

            return _jwtHandler.WriteToken(token);
        }

        public async Task<JwtSecurityToken?> VerifyTokenAsync(string token)
        {
            _logger.LogTrace($"VerifyTokenAsync called (tokenLength: {token?.Length ?? 0}, peer: unknown)");

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning($"VerifyTokenAsync failed - token is null or empty (peer: unknown)");
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityKey = new SymmetricSecurityKey(_jwtOptions.Value.Key);

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
                ClockSkew = TimeSpan.FromMinutes(1) // Допустимый сдвиг времени для компенсации рассинхронизации часов
            };

            try
            {
                // Метод ValidateToken проверяет подпись, время действия (Lifetime), Issuer и Audience
                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken validatedJwtToken)
                {
                    _logger.LogWarning($"VerifyTokenAsync failed - token is not a JwtSecurityToken (peer: unknown)");
                    return null;
                }

                if (await IsTokenRevokedAsync(validatedJwtToken))
                {
                    _logger.LogWarning($"VerifyTokenAsync failed - token is revoked (tokenId: {validatedJwtToken.Id}, peer: unknown)");
                    return null;
                }
                _logger.LogInformation($"Token verified successfully (tokenId: {validatedJwtToken.Id}, subject: {validatedJwtToken.Subject}, peer: unknown)");
                return validatedJwtToken;
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogWarning($"VerifyTokenAsync failed - token is expired (tokenId: redacted, error: {ex.Message}, peer: unknown)");
                return null;
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                _logger.LogWarning($"VerifyTokenAsync failed - token signature is invalid (tokenId: redacted, error: {ex.Message}, peer: unknown)");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"VerifyTokenAsync failed - token validation failed (tokenId: redacted, error: {ex.Message}, peer: unknown)");
                return null;
            }
        }
        public async Task<bool> RevokeTokenAsync(JwtSecurityToken jwtToken)
        {
            _logger.LogTrace($"RevokeTokenAsync called (tokenId: {jwtToken.Id}, subject: {jwtToken.Subject}, type: {jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value}, peer: unknown)");
            try
            {
                if(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == null)
                {
                    _logger.LogError($"RevokeTokenAsync failed - token type claim not found (tokenId: {jwtToken.Id}, peer: unknown)");
                    throw new InvalidOperationException("Token type claim not found");
                }
                if(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == "access")
                {
                    if (!DateTime.TryParse(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value, out DateTime expirationTime))
                    {
                        _logger.LogError($"RevokeTokenAsync failed - token expiration not found (tokenId: {jwtToken.Id}, peer: unknown)");
                        return true;
                    }
                    TimeSpan ttl = expirationTime - DateTime.UtcNow;
                    if (ttl < TimeSpan.Zero)
                    {
                        _logger.LogTrace($"RevokeTokenAsync - token is expired (tokenId: {jwtToken.Id}, ttl: {ttl.TotalSeconds}s, peer: unknown)");
                        return true;
                    }
                    _logger.LogTrace($"RevokeTokenAsync - adding token to blacklist (tokenId: {jwtToken.Id}, ttl: {ttl.TotalSeconds}s, peer: unknown)");
                    await _redis.StringSetAsync($"blacklist:{jwtToken.Id}", jwtToken.Subject.ToString(), ttl);
                    _logger.LogInformation($"Token revoked successfully (tokenId: {jwtToken.Id}, subject: {jwtToken.Subject}, peer: unknown)");
                    return true;
                }
                else
                {
                    _logger.LogTrace($"RevokeTokenAsync - token is refresh token, removing from database (tokenId: {jwtToken.Id}, peer: unknown)");
                    UserJwt? userJwt = await _dbContext.UserJwtRefreshTokens.FindAsync(jwtToken.Id);
                    if (userJwt == null)
                    {
                        _logger.LogWarning($"RevokeTokenAsync - refresh token not found in database (tokenId: {jwtToken.Id}, peer: unknown)");
                        return true;
                    }
                    _dbContext.UserJwtRefreshTokens.Remove(userJwt);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Refresh token revoked successfully (tokenId: {jwtToken.Id}, peer: unknown)");
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"RevokeTokenAsync failed (tokenId: {jwtToken.Id}, error: {e.Message}, peer: unknown)");
                return false;
            }
        }        
        public async Task<bool> IsTokenRevokedAsync(JwtSecurityToken jwtToken)
        {
            _logger.LogTrace($"IsTokenRevokedAsync called (tokenId: {jwtToken.Id}, subject: {jwtToken.Subject}, type: {jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value}, peer: unknown)");
            if(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == null)
            {
                _logger.LogError($"IsTokenRevokedAsync failed - token type claim not found (tokenId: {jwtToken.Id}, peer: unknown)");
                throw new InvalidOperationException("Token type claim not found");
            }
            if(jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == "access")
            {
                _logger.LogTrace($"IsTokenRevokedAsync - checking access token (tokenId: {jwtToken.Id}, peer: unknown)");
                bool isRevoked = await _redis.KeyExistsAsync($"blacklist:{jwtToken.Id}");
                _logger.LogTrace($"IsTokenRevokedAsync - access token revoked: {isRevoked} (tokenId: {jwtToken.Id}, peer: unknown)");
                return isRevoked;
            }
            else
            {
                _logger.LogTrace($"IsTokenRevokedAsync - checking refresh token (tokenId: {jwtToken.Id}, peer: unknown)");
                UserJwt? userJwt = await _dbContext.UserJwtRefreshTokens.FindAsync(jwtToken.Id);
                bool isRevoked = userJwt == null;
                _logger.LogTrace($"IsTokenRevokedAsync - refresh token revoked: {isRevoked} (tokenId: {jwtToken.Id}, peer: unknown)");
                return isRevoked;
            }
        }        
        public JwtSecurityToken ParseToken(string token)
        {
            return _jwtHandler.ReadJwtToken(token);
        }
    }
}