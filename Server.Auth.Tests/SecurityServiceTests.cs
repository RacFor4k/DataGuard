using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Services;
using StackExchange.Redis;

namespace Server.Auth.Tests;

public class SecurityServiceTests
{
    [Fact]
    public async Task GetNonceToken_ThenVerifyNonceToken_ReturnsTrueOnce()
    {
        var (_, database) = TestSupport.CreateRedisMock();
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var service = new SecurityService(database.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        string token = await service.GetNonceToken();
        bool verified = await service.VerifyNonceToken(token);

        Assert.True(verified);
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public async Task VerifyNonceToken_WhenFormatInvalid_ReturnsFalse()
    {
        var (_, database) = TestSupport.CreateRedisMock();
        var service = new SecurityService(database.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        bool verified = await service.VerifyNonceToken("bad-token");

        Assert.False(verified);
    }

    [Fact]
    public async Task GenerateSaltAndHashPassword_UseConfiguredLengths()
    {
        var (_, database) = TestSupport.CreateRedisMock();
        var service = new SecurityService(database.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        byte[] salt = service.GenerateSalt();
        byte[] hash = await service.HashPasswordAsync("StrongPass1!", salt);

        Assert.Equal(4, salt.Length);
        Assert.Equal(4, hash.Length);
    }
}