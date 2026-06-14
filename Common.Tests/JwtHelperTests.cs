using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Common.Helpers;

namespace Common.Tests;

public class JwtHelperTests
{
    [Fact]
    public void ClaimHelpers_ReturnExpectedValues()
    {
        var jwtId = Guid.NewGuid();
        var token = new JwtSecurityToken(claims:
        [
            new Claim(JwtRegisteredClaimNames.GivenName, "Иван"),
            new Claim(JwtRegisteredClaimNames.FamilyName, "Иванов"),
            new Claim(JwtRegisteredClaimNames.Email, "ivan@example.com"),
            new Claim(JwtRegisteredClaimNames.Jti, jwtId.ToString()),
            new Claim(JwtRegisteredClaimNames.Typ, "access"),
            new Claim("role", "system:owner"),
            new Claim("role", "users")
        ]);

        Assert.Equal("Иван", token.GetName());
        Assert.Equal("Иванов", token.GetSurname());
        Assert.Equal("ivan@example.com", token.GetEmail());
        Assert.Equal(jwtId, token.GetJwtId());
        Assert.True(token.IsAccessToken());
        Assert.Equal(["system:owner", "users"], token.GetGroups().ToArray());
    }

    [Fact]
    public void MissingRequiredClaim_ThrowsInvalidDataException()
    {
        var token = new JwtSecurityToken(claims: []);

        Assert.Throws<InvalidDataException>(() => token.GetName());
        Assert.Throws<InvalidDataException>(() => token.GetSurname());
        Assert.Throws<InvalidDataException>(() => token.GetEmail());
        Assert.Throws<InvalidDataException>(() => token.GetJwtId());
    }
}