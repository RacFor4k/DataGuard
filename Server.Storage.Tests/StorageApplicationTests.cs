using Microsoft.AspNetCore.Mvc.Testing;

namespace Server.Storage.Tests;

public class StorageApplicationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StorageApplicationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
}