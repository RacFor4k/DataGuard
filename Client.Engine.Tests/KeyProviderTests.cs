using Client.Engine.Services;

namespace Client.Engine.Tests;

public class KeyProviderTests
{
    [Fact]
    public async Task GetKeyAsync_WhenKeyMissing_ReturnsNull()
    {
        var provider = new KeyProvider();

        byte[]? result = await provider.GetKeyAsync();

        Assert.Null(result);
        Assert.False(provider.HasKey);
    }

    [Fact]
    public async Task SetKeyAsync_StoresCopyAndClearRemovesKey()
    {
        var provider = new KeyProvider();
        byte[] source = [1, 2, 3, 4];

        await provider.SetKeyAsync(source);
        source[0] = 99;

        byte[]? stored = await provider.GetKeyAsync();
        Assert.NotNull(stored);
        Assert.Equal([1, 2, 3, 4], stored);
        Assert.True(provider.HasKey);

        await provider.ClearKeyAsync();

        Assert.False(provider.HasKey);
        byte[]? afterClear = await provider.GetKeyAsync();
        Assert.Null(afterClear);
    }
}