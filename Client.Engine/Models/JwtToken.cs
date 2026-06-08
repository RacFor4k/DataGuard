using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Client.Engine.Models
{
    public class JwtToken
    {
        public required JwtSecurityToken AccessToken { get; set; }
        public required JwtSecurityToken RefreshToken { get; set; }
    }
}