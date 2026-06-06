using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
        public required string Name { get; set; }
        public required string Surname { get; set; }
        public required string Email { get; set; }
        public required List<string> Groups { get; set; }
        public required string JwtId { get; set; }
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