using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Server.Models;

namespace Server.Services
{
    public class UserAccessor
    {
        public JwtSecurityToken? userJwt { get; set; }
    }
}