using System.Security.Cryptography;
using Client.Engine.Services;
using FluentAssertions;

namespace Client.Engine.Tests;

public class KeyProviderAdditionalTests
{
    [Fact]
    public async Task SetKeyAsync_NullKey_ThrowsArgumentNullException()
    {
        var provider = new KeyProvider();

        var act = () => provider.SetKeyAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("key");
    }

    [Fact]
    public async Task SetKeyAsync_OverwritesPreviousKey()
    {
        var provider = new KeyProvider();
        byte[] firstKey = [1, 2, 3, 4, 5];
        byte[] secondKey = [10, 20, 30, 40, 50];

        await provider.SetKeyAsync(firstKey);
        var retrieved = await provider.GetKeyAsync();
        retrieved.Should().BeEquivalentTo(firstKey);

        await provider.SetKeyAsync(secondKey);
        var retrievedAfter = await provider.GetKeyAsync();
        retrievedAfter.Should().BeEquivalentTo(secondKey, "second SetKeyAsync should replace the first key");
    }

    [Fact]
    public async Task ClearKeyAsync_WhenNoKeySet_IsIdempotent()
    {
        var provider = new KeyProvider();

        // Calling ClearKeyAsync when no key has been set should not throw
        var act = () => provider.ClearKeyAsync();

        await act.Should().NotThrowAsync();
        provider.HasKey.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentSetAndGet_BothCompleteWithoutError()
    {
        var provider = new KeyProvider();
        byte[] key = RandomNumberGenerator.GetBytes(64);
        const int iterations = 50;

        // Pre-set the key so GetKeyAsync succeeds
        await provider.SetKeyAsync(key);

        var tasks = new List<Task>();
        for (int i = 0; i < iterations; i++)
        {
            // Alternate between setting a new key and getting the current key
            if (i % 2 == 0)
            {
                byte[] newKey = RandomNumberGenerator.GetBytes(64);
                tasks.Add(provider.SetKeyAsync(newKey));
            }
            else
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await provider.GetKeyAsync();
                    result.Should().NotBeNull().And.HaveCount(64);
                }));
            }
        }

        // All tasks should complete without exception
        var acting = () => Task.WhenAll(tasks);
        await acting.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetKeyAsync_ModifyingOriginalArray_DoesNotAffectStoredKey()
    {
        var provider = new KeyProvider();
        byte[] original = [10, 20, 30, 40];

        await provider.SetKeyAsync(original);

        // Mutate the original array after setting
        original[0] = 99;
        original[1] = 88;

        var stored = await provider.GetKeyAsync();
        stored.Should().BeEquivalentTo(new byte[] { 10, 20, 30, 40 },
            "the provider should store a defensive copy");
    }

    [Fact]
    public async Task GetKeyAsync_ReturnsDefensiveCopy()
    {
        var provider = new KeyProvider();
        byte[] key = [5, 10, 15, 20];
        await provider.SetKeyAsync(key);

        var copy1 = await provider.GetKeyAsync();
        var copy2 = await provider.GetKeyAsync();

        // They should have the same values but be different object references
        copy1.Should().BeEquivalentTo(copy2);
        copy1.Should().NotBeSameAs(copy2, "each call should return a new defensive copy");
    }

    [Fact]
    public async Task HasKey_ReflectsCurrentState()
    {
        var provider = new KeyProvider();

        provider.HasKey.Should().BeFalse("no key has been set yet");

        await provider.SetKeyAsync([1, 2, 3]);
        provider.HasKey.Should().BeTrue();

        await provider.ClearKeyAsync();
        provider.HasKey.Should().BeFalse("key has been cleared");
    }
}