using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace Server.Models
{
    // nullable поля заполняется во время генерации токена
    public class UserJwt
    {
        public string? Issuer { get; set; }
        public string? Audience { get; set; }
        public DateTime? ExpirationTime { get; set; }
        public required Guid Subject { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Email { get; set; }
        public List<string>? Groups { get; set; }
        public string JwtId { get; set; } = Guid.CreateVersion7().ToString();
        [MemberNotNullWhen(true, nameof(Name), nameof(Surname), nameof(Email), nameof(Groups))]
        public bool IsAccessToken()
        {
            return Name != null && Surname != null && Email != null && Groups != null;
        }
    }

    [Table("UserJwtRefreshTokens", Schema = "identity")]
    public class DbUserJwt
    {
        [Key]
        public required string JwtId { get; set; }
        public required string UserId { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime LastAccessed { get; set; }
        public required string IpAddr { get; set; }
        public required string UserAgent { get; set; }
        public required string MachineName { get; set; }

    }
}