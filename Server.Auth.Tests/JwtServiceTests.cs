using System.IdentityModel.Tokens.Jwt;
using Common.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Services;
using StackExchange.Redis;

namespace Server.Auth.Tests;

public class JwtServiceTests
{
    [Fact]
    public async Task GenerateAccessToken_ThenVerifyToken_ReturnsJwtWithClaims()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, database) = TestSupport.CreateRedisMock();
        database.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        string token = service.GenerateAccessToken(Guid.NewGuid().ToString(), "Иван", "Иванов", "ivan@example.com", ["system:owner"]);
        JwtSecurityToken? verified = await service.VerifyTokenAsync(token);

        Assert.NotNull(verified);
        Assert.True(verified.IsAccessToken());
        Assert.Equal("Иван", verified.GetName());
        Assert.Contains("system:owner", verified.GetGroups());
    }

    [Fact]
    public async Task RevokeTokenAsync_ForAccessToken_AddsTokenToRedisBlacklist()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, database) = TestSupport.CreateRedisMock();
        RedisKey storedKey = default;
        TimeSpan? storedTtl = null;
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When>((key, _, ttl, _) =>
            {
                storedKey = key;
                storedTtl = ttl;
            })
            .ReturnsAsync(true);
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, _, ttl, _, _) =>
            {
                storedKey = key;
                storedTtl = ttl;
            })
            .ReturnsAsync(true);
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>((key, _, _, _, _) =>
            {
                storedKey = key;
                storedTtl = TimeSpan.FromSeconds(1);
            })
            .ReturnsAsync(true);
        var service = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);
        var token = service.ParseToken(service.GenerateAccessToken(Guid.NewGuid().ToString(), "Иван", "Иванов", "ivan@example.com", []));

        bool revoked = await service.RevokeTokenAsync(token);

        Assert.True(revoked);
        Assert.Contains($"blacklist:{token.Id}", storedKey.ToString());
        Assert.NotNull(storedTtl);
        Assert.True(storedTtl > TimeSpan.Zero);
    }
}