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
        public async Task<string> GenerateAccessTokenAsync(UserJwt userJwt) 
        {
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

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        
        public async Task<string> GenerateRefreshTokenAsync(UserJwt userJwt)
        {
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

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<IEnumerable<Claim>?> VerifyTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || await IsTokenBlacklistedAsync(token))
            {
                return null;
            }

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found in appsettings.json")));
            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidIssuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not found in appsettings.json"),
                    ValidAudience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not found in appsettings.json"),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = securityKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                }, out SecurityToken validatedToken);
                return ((JwtSecurityToken)validatedToken).Claims;
            }
            catch(Exception e)
            {
                _logger.LogInformation($"Token validation failed: {e.Message}");
                return null;
            }
        }

        public async Task<UserJwt?> ParceTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                JwtSecurityToken jwtSecurityToken = tokenHandler.ReadJwtToken(token);

                string issuer = jwtSecurityToken.Issuer;
                string audience = String.Join(",", jwtSecurityToken.Audiences);
                DateTime expirationTime = jwtSecurityToken.ValidTo;
                Guid subject = Guid.Parse(jwtSecurityToken.Subject);
                string name = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value ?? string.Empty;
                string surname = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value ?? string.Empty;
                string email = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;
                List<string> groups = jwtSecurityToken.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToList();
                string jwtId = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value ?? string.Empty;

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
                    JwtId = jwtId
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"Token parsing failed: {e.Message}");
                return null;
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                UserJwt userJwt = await ParceTokenAsync(token) ?? throw new InvalidOperationException("Token is invalid");
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

        public async Task<bool> IsTokenBlacklistedAsync(string token)
        {
            UserJwt? userJwt = await ParceTokenAsync(token);
            if (userJwt == null)
            {
                return false;
            }
            return await _redis.KeyExistsAsync($"blacklist:{userJwt.JwtId}");
        }

        public async Task AddTokenToBlacklistAsync(string token)
        {
            UserJwt? userJwt = await ParceTokenAsync(token);
            if (userJwt == null)
            {
                throw new InvalidOperationException("Token is invalid");
            }
            DateTime expirationTime = userJwt.ExpirationTime ?? throw new InvalidOperationException("Token expiration time is invalid");
            TimeSpan ttl = expirationTime - DateTime.UtcNow;
            if (ttl < TimeSpan.Zero)
            {
                throw new InvalidOperationException("Token is expired");
            }
            await _redis.StringSetAsync($"blacklist:{userJwt.JwtId}", token, ttl);
        }

    }
}