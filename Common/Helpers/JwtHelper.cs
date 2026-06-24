using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Common.Helpers
{
    public static class JwtHelper
    {
        public static string GetName(this JwtSecurityToken token) => 
            token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value 
            ?? throw new InvalidDataException("Name claim not found");

        public static string GetSurname(this JwtSecurityToken token) => 
            token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value 
            ?? throw new InvalidDataException("Surname claim not found");

        public static string GetEmail(this JwtSecurityToken token) => 
            token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value 
            ?? throw new InvalidDataException("Email claim not found");

        public static IEnumerable<string> GetGroups(this JwtSecurityToken token) => 
            token.Claims
                .Where(c => c.Type == "role")
                .Select(c => c.Value);

        public static Guid GetJwtId(this JwtSecurityToken token)
        {
            var jti = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value 
                ?? throw new InvalidDataException("JwtId claim not found");

            if (!Guid.TryParse(jti, out var guid))
            {
                throw new InvalidDataException("JwtId is not a valid Guid");
            }

            return guid;
        }

        public static bool IsAccessToken(this JwtSecurityToken token)
        {
            return token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value == "access";
        }
    }
}