using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Client.Engine.Models
{
    [Table("jwt_tokens")]
    public class JwtToken
    {
        public Guid Id { get; set; }
        public required string AccessToken { get; set; }
        public required string RefreshToken {get;set;}
        public required Guid AccountId { get; set; }
        public required Account Account { get; set; }
        private static readonly JwtSecurityTokenHandler TokenHandler = new();
        [NotMapped]
        public JwtSecurityToken DecodedAccessToken => TokenHandler.ReadJwtToken(AccessToken);
        [NotMapped]
        public JwtSecurityToken DecodedRefreshToken => TokenHandler.ReadJwtToken(RefreshToken);
    }
}