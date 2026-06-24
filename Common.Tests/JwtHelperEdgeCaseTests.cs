using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Common.Helpers;
using FluentAssertions;

namespace Common.Tests;

public class JwtHelperEdgeCaseTests
{
    [Fact]
    public void GetGroups_WithNoRoleClaims_ReturnsEmpty()
    {
        var token = new JwtSecurityToken(claims:
        [
            new Claim(JwtRegisteredClaimNames.GivenName, "Test"),
            new Claim(JwtRegisteredClaimNames.FamilyName, "User"),
            new Claim(JwtRegisteredClaimNames.Email, "test@example.com"),
        ]);

        var groups = token.GetGroups();

        groups.Should().BeEmpty();
    }

    [Fact]
    public void GetGroups_WithMultipleRoles_ReturnsAll()
    {
        var token = new JwtSecurityToken(claims:
        [
            new Claim(JwtRegisteredClaimNames.GivenName, "Test"),
            new Claim(JwtRegisteredClaimNames.FamilyName, "User"),
            new Claim(JwtRegisteredClaimNames.Email, "test@example.com"),
            new Claim("role", "admin"),
            new Claim("role", "editor"),
            new Claim("role", "viewer"),
        ]);

        var groups = token.GetGroups();

        groups.Should().BeEquivalentTo(["admin", "editor", "viewer"]);
    }

    [Fact]
    public void GetJwtId_WithNonGuidJti_ThrowsInvalidDataException()
    {
        var token = new JwtSecurityToken(claims:
        [
            new Claim(JwtRegisteredClaimNames.Jti, "not-a-guid"),
        ]);

        var act = () => token.GetJwtId();

        act.Should().Throw<InvalidDataException>()
            .WithMessage("JwtId is not a valid Guid");
    }

    [Fact]
    public void IsAccessToken_WithTypRefresh_ReturnsFalse()
    {
        var token = new JwtSecurityToken(claims:
        [
            new Claim(JwtRegisteredClaimNames.Typ, "refresh"),
        ]);

        token.IsAccessToken().Should().BeFalse();
    }

    [Fact]
    public void IsAccessToken_WithMissingTypClaim_ReturnsFalse()
    {
        var token = new JwtSecurityToken(claims:
        [
            new Claim(JwtRegisteredClaimNames.Sub, "user123"),
        ]);

        token.IsAccessToken().Should().BeFalse();
    }
}