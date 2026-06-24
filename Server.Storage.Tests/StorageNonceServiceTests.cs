using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageNonceServiceTests
{
    private readonly Mock<IDatabase> _mockDb = new();
    private readonly Mock<IConnectionMultiplexer> _mockRedis = new();
    private readonly StorageNonceService _service;

    public StorageNonceServiceTests()
    {
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDb.Object);
        _service = new StorageNonceService(_mockRedis.Object);
    }

    [Fact]
    public async Task TryConsumeNonceAsync_ValidNonce_ReturnsTrue()
    {
        var ownerId = Guid.NewGuid();

        _mockDb.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(true);

        var result = await _service.TryConsumeNonceAsync(ownerId, "DeleteFile", "nonce-abc", TimeSpan.FromMinutes(5));

        result.Should().BeTrue();
        _mockDb.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("nonce-abc")),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            When.NotExists), Times.Once);
    }

    [Fact]
    public async Task TryConsumeNonceAsync_SameNonceAgain_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();

        // First call succeeds, second fails (key already exists)
        var call = 0;
        _mockDb.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(() => call++ == 0);

        var first = await _service.TryConsumeNonceAsync(ownerId, "DeleteFile", "nonce-abc", TimeSpan.FromMinutes(5));
        var second = await _service.TryConsumeNonceAsync(ownerId, "DeleteFile", "nonce-abc", TimeSpan.FromMinutes(5));

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeNonceAsync_ExpiredNonce_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();

        _mockDb.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(false);

        var result = await _service.TryConsumeNonceAsync(ownerId, "DeleteFile", "old-nonce", TimeSpan.FromMinutes(5));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeNonceAsync_DifferentOperationsCoexist()
    {
        var ownerId = Guid.NewGuid();
        var nonce = "same-nonce-token";

        var callCount = 0;
        _mockDb.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return true;
            });

        var deleteResult = await _service.TryConsumeNonceAsync(ownerId, "DeleteFile", nonce, TimeSpan.FromMinutes(5));
        var updateResult = await _service.TryConsumeNonceAsync(ownerId, "UpdateFile", nonce, TimeSpan.FromMinutes(5));

        deleteResult.Should().BeTrue();
        updateResult.Should().BeTrue();
        _mockDb.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            When.NotExists), Times.Exactly(2));
    }

    [Fact]
    public async Task TryConsumeNonceAsync_KeyContainsOperationName()
    {
        var ownerId = Guid.NewGuid();

        _mockDb.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(true);

        await _service.TryConsumeNonceAsync(ownerId, "MoveFile", "my-nonce", TimeSpan.FromMinutes(5));

        _mockDb.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("MoveFile")),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            When.NotExists), Times.Once);
    }
}