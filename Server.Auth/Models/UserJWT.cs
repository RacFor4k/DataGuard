using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace Server.Auth.Models
{

    [Table("UserJwtRefreshTokens", Schema = "identity")]
    public class UserJwt
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