using System;
using System.Collections.Generic;
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
            if(await _jwtService.VerifyTokenAsync(token) == null)
            {
                await _next(context);
                return;
            } 
            UserJwt? userJwt = _jwtService.ParceToken(token);
            if (userJwt == null)
            {
                await _next(context);
                return;
            }
            _userAccessor.User = userJwt;
            await _next(context);
        }
    }
}