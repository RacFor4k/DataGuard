using FluentAssertions;
using Minio;
using Moq;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class MinioBlobStoreTests
{
    private readonly Mock<IMinioClient> _mockMinio = new();
    private readonly MinioBlobStore _store;

    public MinioBlobStoreTests()
    {
        _store = new MinioBlobStore(_mockMinio.Object);
    }

    [Fact]
    public void GenerateStorageKey_ReturnsNonEmptyString()
    {
        var key = _store.GenerateStorageKey("txt");

        key.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateStorageKey_ContainsFileExtension()
    {
        var key = _store.GenerateStorageKey(".csv");

        key.Should().EndWith(".csv");
    }

    [Fact]
    public void GenerateStorageKey_AddsDotWhenMissing()
    {
        var key = _store.GenerateStorageKey("csv");

        key.Should().EndWith(".csv");
    }

    [Fact]
    public void GenerateStorageKey_DifferentCalls_ProduceDifferentKeys()
    {
        var key1 = _store.GenerateStorageKey("txt");
        var key2 = _store.GenerateStorageKey("txt");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateStorageKey_EmptyExtension_NoExtensionAppended()
    {
        var key = _store.GenerateStorageKey("");

        key.Should().NotContain(".");
    }

    [Fact]
    public void GenerateStorageKey_NullExtension_NoExtensionAppended()
    {
        var key = _store.GenerateStorageKey(null!);

        key.Should().NotContain(".");
    }
}