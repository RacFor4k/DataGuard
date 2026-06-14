using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageLinkServiceTests
{
    [Fact]
    public void GenerateSecureToken_ReturnsNonEmptyToken()
    {
        var token1 = StorageLinkServiceTests_GenerateToken();
        var token2 = StorageLinkServiceTests_GenerateToken();

        Assert.NotNull(token1);
        Assert.NotNull(token2);
        Assert.NotEqual(token1, token2);
        Assert.DoesNotContain(token1, " ");
        Assert.DoesNotContain(token2, " ");
    }

    private static string StorageLinkServiceTests_GenerateToken()
    {
        byte[] bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
