using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Server.Interfaces;
using Server.Models;
using Server.Services;

namespace Server.Middlewares
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IJwtService _jwtService;
        private readonly UserAccessor _userAccessor;

        public JwtMiddleware(RequestDelegate next, IJwtService jwtService, UserAccessor userAccessor)
        {
            _next = next;
            _jwtService = jwtService;
            _userAccessor = userAccessor;
        }

        public async Task Invoke(HttpContext context)
        {
            string? authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null)
            {
                await _next(context);
                return;
            }
            if (!authHeader.StartsWith("Bearer "))
            {
                await _next(context);
                return;
            }
            string token = authHeader.Substring(7);
            _userAccessor.userJwt = await _jwtService.VerifyTokenAsync(token);
            await _next(context);
        }
    }
}