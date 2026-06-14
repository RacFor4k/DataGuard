using Client.Engine.Services;
using Microsoft.Extensions.Configuration;

namespace Client.Engine.Tests;

public class UserAgentProviderTests
{
    [Fact]
    public void GetUserAgent_IncludesProductVersionAndPlatformInfo()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ClientVersion"] = "1.2.3" })
            .Build();

        var provider = new UserAgentProvider(configuration);

        string userAgent = provider.GetUserAgent();

        Assert.StartsWith("DataGuardClient/1.2.3 (", userAgent);
        Assert.Contains(";", userAgent);
    }
}