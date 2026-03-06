using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Server.Modules
{
    public interface IJwtModule
    {
        string GenerateAccessToken(int userId);
        string GenerateRefreshToken();
        TokenValidationResult ValidateRefreshToken(string refreshToken);
    }

    public class JwtModule : IJwtModule
    {
        private readonly string _secretKey;
        private readonly int _accessTokenExpirationMinutes;
        private readonly string _issuer;
        private readonly string _audience;

        public JwtModule(IConfiguration configuration)
        {
            _secretKey = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey не настроен");
            _accessTokenExpirationMinutes = configuration.GetValue("Jwt:AccessTokenExpirationMinutes", 15);
            _issuer = configuration["Jwt:Issuer"] ?? "DataGuard";
            _audience = configuration["Jwt:Audience"] ?? "DataGuard";
        }

        public string GenerateAccessToken(int userId)
        {
            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public TokenValidationResult ValidateRefreshToken(string refreshToken)
        {
            // Базовая валидация refresh токена (проверка формата)
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return new TokenValidationResult { IsValid = false, Exception = new SecurityTokenException("Refresh token пуст") };
            }

            try
            {
                // Пробуем декодировать Base64
                Convert.FromBase64String(refreshToken);
                return new TokenValidationResult { IsValid = true };
            }
            catch (FormatException)
            {
                return new TokenValidationResult { IsValid = false, Exception = new SecurityTokenException("Неверный формат refresh token") };
            }
        }
    }

    public class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public Exception? Exception { get; set; }
    }
}
