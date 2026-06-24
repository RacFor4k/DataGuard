using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Server.Auth.Interfaces;
using Common.Server.Models;
using Server.Auth.Services;

namespace Server.Auth.Middlewares
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IJwtService jwtService, UserAccessor userAccessor)
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
            userAccessor.UserJwt = await jwtService.VerifyTokenAsync(token);
            await _next(context);
        }
    }
}