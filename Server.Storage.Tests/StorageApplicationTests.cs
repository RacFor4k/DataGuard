using Microsoft.AspNetCore.Mvc.Testing;

namespace Server.Storage.Tests;

public class StorageApplicationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StorageApplicationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WeatherForecast_WithoutAuthentication_IsNotSuccessful()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/WeatherForecast");

        Assert.False(response.IsSuccessStatusCode);
    }
}