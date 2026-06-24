using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Common.Helpers;
using Common.Server.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Services;
using StackExchange.Redis;

namespace Server.Auth.Tests;

public class JwtServiceAdditionalTests
{
    [Fact]
    public async Task GenerateRefreshToken_ContainsTypRefreshClaim()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        string token = service.GenerateRefreshToken(Guid.NewGuid().ToString(), "Name", "Surname", "a@b.com", ["group1"]);

        var parsed = service.ParseToken(token);
        parsed.IsAccessToken().Should().BeFalse("refresh tokens should not be access tokens");
        parsed.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value.Should().Be("refresh");
    }

    [Fact]
    public async Task VerifyTokenAsync_WithNullToken_ReturnsNull()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var result = await service.VerifyTokenAsync(null!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyTokenAsync_WithEmptyToken_ReturnsNull()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var result = await service.VerifyTokenAsync(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyTokenAsync_WithWhitespaceToken_ReturnsNull()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var result = await service.VerifyTokenAsync("   ");

        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyTokenAsync_WithTamperedToken_ReturnsNull()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, database) = TestSupport.CreateRedisMock();
        database.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        string token = service.GenerateAccessToken(Guid.NewGuid().ToString(), "Name", "Surname", "a@b.com", []);
        // Tamper with the token by modifying characters near the end
        string tampered = token[..^5] + "XXXXX";

        var result = await service.VerifyTokenAsync(tampered);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeTokenAsync_ForRefreshToken_DeletesUserJwtFromDb()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var jti = Guid.NewGuid().ToString();
        // Create a UserJwt in DB that matches the refresh token's jti
        db.UserJwtRefreshTokens.Add(new UserJwt
        {
            JwtId = jti,
            UserId = Guid.NewGuid().ToString(),
            RefreshToken = "refresh-token",
            LastAccessed = DateTime.UtcNow,
            IpAddr = "127.0.0.1",
            UserAgent = "test",
            MachineName = "test"
        });
        await db.SaveChangesAsync();

        // Build a custom JwtSecurityToken with the matching jti and typ=refresh
        var baseToken = service.ParseToken(service.GenerateRefreshToken(Guid.NewGuid().ToString(), "N", "S", "a@b.com", []));
        var customClaims = baseToken.Claims
            .Where(c => c.Type != JwtRegisteredClaimNames.Jti)
            .Concat([new Claim(JwtRegisteredClaimNames.Jti, jti)]);
        var customToken = new JwtSecurityToken(
            baseToken.Claims.First(c => c.Type == "iss").Value,
            baseToken.Claims.First(c => c.Type == "aud").Value,
            customClaims,
            baseToken.ValidFrom,
            baseToken.ValidTo,
            baseToken.SigningCredentials);

        bool revoked = await service.RevokeTokenAsync(customToken);

        revoked.Should().BeTrue();
        db.UserJwtRefreshTokens.Find(jti).Should().BeNull("the refresh token should be deleted from DB");
    }

    [Fact]
    public async Task IsTokenRevokedAsync_WithAccessToken_ChecksRedisBlacklist()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, database) = TestSupport.CreateRedisMock();
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var token = service.ParseToken(service.GenerateAccessToken(Guid.NewGuid().ToString(), "Name", "Surname", "a@b.com", []));

        database
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        bool isRevoked = await service.IsTokenRevokedAsync(token);

        isRevoked.Should().BeTrue("access token should be checked against Redis blacklist");
        database.Verify(d => d.KeyExistsAsync($"jwt:blacklist:{token.Id}", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task IsTokenRevokedAsync_NonRevokedToken_ReturnsFalse()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, database) = TestSupport.CreateRedisMock();
        database.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var token = service.ParseToken(service.GenerateAccessToken(Guid.NewGuid().ToString(), "Name", "Surname", "a@b.com", []));

        bool isRevoked = await service.IsTokenRevokedAsync(token);

        isRevoked.Should().BeFalse("non-revoked access token should return false");
    }
}