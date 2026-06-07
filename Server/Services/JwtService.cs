using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Server.Models;
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
        private readonly IConfiguration _config;
        public ILogger<JwtService> _logger { get; set; }

        public JwtService(DataGuardDbContext dbContext, IConnectionMultiplexer redis, IConfiguration config, ILogger<JwtService> logger)
        {
            _dbContext = dbContext;
            _redis = redis.GetDatabase().WithKeyPrefix("jwt:");
            _config = config;
            _logger = logger;
        }
        /// <summary>
        /// Генерирует Access токен.
        /// Заполянется nullable поля в UserJwt.
        /// </summary>
        /// <param name="userJwt">UserJwt объект.</param>
        /// <returns>Access токен.</returns>
        public Task<string> GenerateAccessTokenAsync(UserJwt userJwt) 
        {
            if (!userJwt.IsAccessToken())
            {
                throw new InvalidOperationException("Token is not an access token");
            }
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found in appsettings.json")));
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            userJwt.Issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not found in appsettings.json");
            userJwt.Audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not found in appsettings.json");
            userJwt.ExpirationTime = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:AccessTokenExpirationMinutes"] ?? throw new InvalidOperationException("JWT AccessTokenExpirationMinutes not found in appsettings.json")));
            
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userJwt.Subject.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, string.Concat(userJwt.Name, " ", userJwt.Surname)),
                new Claim(JwtRegisteredClaimNames.GivenName, userJwt.Name),
                new Claim(JwtRegisteredClaimNames.FamilyName, userJwt.Surname),
                new Claim(JwtRegisteredClaimNames.Email, userJwt.Email),
                new Claim(JwtRegisteredClaimNames.Jti, userJwt.JwtId),
                new Claim("roles", string.Join(",", userJwt.Groups))
            };
            
            JwtSecurityToken token = new JwtSecurityToken(
                issuer: userJwt.Issuer,
                audience: userJwt.Audience,
                claims: claims,
                expires: userJwt.ExpirationTime,
                signingCredentials: credentials);

            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }
        
        public Task<string> GenerateRefreshTokenAsync(UserJwt userJwt)
        {
            if (userJwt.IsAccessToken())
            {
                throw new InvalidOperationException("Token is an access token");
            }
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found in appsettings.json")));
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            userJwt.Issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not found in appsettings.json");
            userJwt.ExpirationTime = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:RefreshTokenExpirationMinutes"] ?? throw new InvalidOperationException("JWT AccessTokenExpirationMinutes not found in appsettings.json")));
            
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userJwt.Subject.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, userJwt.JwtId),
            };
            
            JwtSecurityToken token = new JwtSecurityToken(
                issuer: userJwt.Issuer,
                audience: userJwt.Audience,
                claims: claims,
                expires: userJwt.ExpirationTime,
                signingCredentials: credentials);

            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }

        public async Task<UserJwt?> VerifyTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Получение настроек из конфигурации
            var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found in appsettings.json");
            var issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not found in appsettings.json");
            var audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not found in appsettings.json");
            
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            var validationParameters = new TokenValidationParameters
            {
                // Проверка подписи
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,

                // Проверка издателя и потребителя
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,

                // Проверка времени действия токена (экспирация)
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

                UserJwt? userJwt = ParceToken(validatedJwtToken);
                if (userJwt == null)
                {
                    return null;
                }

                if (await IsTokenBlacklistedAsync(userJwt))
                {
                    return null;
                }
                return userJwt;
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

        public UserJwt? ParceToken(JwtSecurityToken jwtSecurityToken)
        {
            if (jwtSecurityToken == null)
            {
                return null;
            }

            try
            {
                string issuer = jwtSecurityToken.Issuer;
                string audience = string.Join(",", jwtSecurityToken.Audiences);
                DateTime expirationTime = jwtSecurityToken.ValidTo;
                
                Guid subject = Guid.Parse(jwtSecurityToken.Subject);
                
                string? name = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value;
                string? surname = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value;
                string? email = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
                List<string> groups = jwtSecurityToken.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToList();
                
                string jwtId = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value 
                            ?? throw new InvalidOperationException("JWT Id is empty");

                return new UserJwt
                {
                    Issuer = issuer,
                    Audience = audience,
                    ExpirationTime = expirationTime,
                    Subject = subject,
                    Name = name,
                    Surname = surname,
                    Email = email,
                    Groups = groups,
                    JwtId = jwtId,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"Token mapping failed: {e.Message}");
                return null;
            }
        }
        public UserJwt? ParceToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                var jwtSecurityToken = tokenHandler.ReadJwtToken(token);
                
                return ParceToken(jwtSecurityToken);
            }
            catch (Exception e)
            {
                _logger.LogError($"Token parsing failed: {e.Message}");
                return null;
            }
        }

        public async Task<bool> RevokeTokenAsync(UserJwt userJwt)
        {
            try
            {
                DbUserJwt? dbUserJwt = await _dbContext.UserJwtRefreshTokens.FindAsync(userJwt.JwtId);
                if (dbUserJwt == null)
                {
                    return true;
                }
                _dbContext.UserJwtRefreshTokens.Remove(dbUserJwt);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (InvalidOperationException e)
            {
                _logger.LogInformation($"Token revoking failed: {e.Message}");
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError($"Token revoking failed: {e.Message}");
                return false;
            }
        }
        public async Task<bool> RevokeTokenAsync(string token)
        {
            UserJwt? userJwt = ParceToken(token);
            if (userJwt == null)
            {
                return false;
            }
            return await RevokeTokenAsync(userJwt);
        }

        public async Task<bool> IsTokenBlacklistedAsync(UserJwt userJwt)
        {
            if(userJwt.IsAccessToken())
                return await _redis.KeyExistsAsync($"blacklist:{userJwt.JwtId}");
            else
            {
                DbUserJwt? dbToken = await _dbContext.UserJwtRefreshTokens.FindAsync(userJwt.JwtId);
                if (dbToken == null)
                {
                    return false;
                }
                return true;
            }
        }

        public async Task AddTokenToBlacklistAsync(UserJwt userJwt)
        {
            DateTime expirationTime = userJwt.ExpirationTime ?? throw new InvalidOperationException("Token expiration time is invalid");
            TimeSpan ttl = expirationTime - DateTime.UtcNow;
            if (ttl < TimeSpan.Zero)
            {
                return;
            }
            await _redis.StringSetAsync($"blacklist:{userJwt.JwtId}", userJwt.Subject.ToString(), ttl);
        }

    }
}