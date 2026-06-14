using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Server.Auth.Models;
using Microsoft.Extensions.Logging;

namespace Server.Auth.Services
{
    public class UserAccessor
    {
        private readonly ILogger<UserAccessor> _logger;
        public JwtSecurityToken? userJwt { get; set; }
        public UserAccessor(ILogger<UserAccessor> logger)
        {
            _logger = logger;
            _logger.LogTrace($"UserAccessor initialized (peer: unknown)");
        }
    }
}