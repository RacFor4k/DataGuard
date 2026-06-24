using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Services;
using StackExchange.Redis;

namespace Server.Auth.Tests;

public class SecurityServiceAdditionalTests
{
    [Fact]
    public async Task VerifyNonceToken_WithExpiredToken_ReturnsFalse()
    {
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        // Create a token with an expiration in the past
        long pastExpiration = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        string nonce = Guid.NewGuid().ToString();
        string tokenPayload = $"{pastExpiration}.{nonce}";
        string signature = Convert.ToBase64String(HMACSHA256.HashData((byte[])[1, 2, 3, 4, 5, 6, 7, 8], (byte[])Encoding.UTF8.GetBytes(tokenPayload)));
        string token = $"{tokenPayload}.{signature}";

        bool result = await service.VerifyNonceToken(token);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyNonceToken_WithTamperedSignature_ReturnsFalse()
    {
        var (multiplexer, database) = TestSupport.CreateRedisMock();
        // The token is well-formed but signature is wrong, so Redis key won't be deleted
        database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        var service = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        long futureExpiration = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        string nonce = Guid.NewGuid().ToString();
        string tokenPayload = $"{futureExpiration}.{nonce}";
        string tamperedSignature = Convert.ToBase64String([0, 0, 0, 0]);
        string token = $"{tokenPayload}.{tamperedSignature}";

        bool result = await service.VerifyNonceToken(token);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyNonceToken_Reused_ReturnsFalseOnSecondCall()
    {
        var (multiplexer, database) = TestSupport.CreateRedisMock();
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        // First call deletes the key (returns true), second call fails to delete (returns false)
        var callCount = 0;
        database
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1;
            });
        var service = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        string token = await service.GetNonceToken();
        bool firstResult = await service.VerifyNonceToken(token);
        bool secondResult = await service.VerifyNonceToken(token);

        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyNonceToken_WithNullOrEmpty_ReturnsFalse(string? token)
    {
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        bool result = await service.VerifyNonceToken(token!);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HashPasswordAsync_SameInput_ReturnsSameOutput()
    {
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        byte[] salt = [1, 2, 3, 4];
        byte[] password = Encoding.UTF8.GetBytes("password123");

        byte[] hash1 = await service.HashPasswordAsync(password, salt);
        byte[] hash2 = await service.HashPasswordAsync(password, salt);

        hash1.Should().BeEquivalentTo(hash2);
    }

    [Fact]
    public async Task HashPasswordAsync_DifferentData_ReturnsDifferentOutput()
    {
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var service = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, TestSupport.CreateSecurityOptions());

        byte[] salt = [1, 2, 3, 4];
        byte[] password1 = Encoding.UTF8.GetBytes("password123");
        byte[] password2 = Encoding.UTF8.GetBytes("different456");

        byte[] hash1 = await service.HashPasswordAsync(password1, salt);
        byte[] hash2 = await service.HashPasswordAsync(password2, salt);

        hash1.Should().NotBeEquivalentTo(hash2);
    }
}