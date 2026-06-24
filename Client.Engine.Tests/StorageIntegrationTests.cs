using Client.Engine.Interfaces;
using Client.Engine.Models;
using Client.Engine.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Client.Engine.Tests.Integration;

[CollectionDefinition("Storage Integration Tests")]
public class StorageIntegrationTestCollection : ICollectionFixture<StorageTestFixture>
{
}

[Collection("Storage Integration Tests")]
public class StorageIntegrationTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture;
    private readonly IStorageService _storageService;

    public StorageIntegrationTests(StorageTestFixture fixture)
    {
        _fixture = fixture;
        _storageService = fixture.ServiceProvider.GetRequiredService<IStorageService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StorageService_WithoutAuth_ReturnsUnauthenticated()
    {
        var result = await _storageService.GetMetadataAsync(Guid.NewGuid());
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UploadFile_EmptyName_ReturnsError()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _storageService.UploadFileAsync(stream, "", "docs");

        Assert.False(result.Success);
        Assert.Contains("пустым", result.Message);
    }

    [Fact]
    public async Task UploadFile_InvalidPath_ReturnsError()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _storageService.UploadFileAsync(stream, "file.txt", "../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task MoveFile_InvalidPath_ReturnsError()
    {
        var result = await _storageService.MoveFileAsync(Guid.NewGuid(), "../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task CopyFile_InvalidPath_ReturnsError()
    {
        var result = await _storageService.CopyFileAsync(Guid.NewGuid(), "../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task RenameFile_EmptyName_ReturnsError()
    {
        var result = await _storageService.RenameFileAsync(Guid.NewGuid(), "");

        Assert.False(result.Success);
        Assert.Contains("пустым", result.Message);
    }

    [Fact]
    public async Task NewDirectory_EmptyPath_ReturnsError()
    {
        var result = await _storageService.NewDirectoryAsync("");

        Assert.False(result.Success);
        Assert.Contains("пустым", result.Message);
    }

    [Fact]
    public async Task NewDirectory_TraversalPath_ReturnsError()
    {
        var result = await _storageService.NewDirectoryAsync("../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task UpdateMetadata_ReservedKey_ReturnsError()
    {
        var metadata = new Dictionary<string, string> { { "storageKey", "value" } };
        var result = await _storageService.UpdateMetadataAsync(Guid.NewGuid(), metadata);

        Assert.False(result.Success);
        Assert.Contains("зарезервирован", result.Message);
    }

    [Fact]
    public async Task GenerateLink_InvalidTtl_ReturnsError()
    {
        var result = await _storageService.GenerateLinkAsync(Guid.NewGuid(), ttlSeconds: -1);

        Assert.False(result.Success);
        Assert.Contains("положительным", result.Message);
    }

    [Fact]
    public async Task GenerateLink_TtlTooLarge_ReturnsError()
    {
        var result = await _storageService.GenerateLinkAsync(Guid.NewGuid(), ttlSeconds: 2592001);

        Assert.False(result.Success);
        Assert.Contains("30 дней", result.Message);
    }

    [Fact]
    public async Task UpdateFile_NegativeOffset_ReturnsError()
    {
        var result = await _storageService.UpdateFileAsync(Guid.NewGuid(), -1, data: new byte[] { 1 });

        Assert.False(result.Success);
        Assert.Contains("offset", result.Message);
    }

    [Fact]
    public async Task UpdateFile_NoDataOrErase_ReturnsError()
    {
        var result = await _storageService.UpdateFileAsync(Guid.NewGuid(), 0);

        Assert.False(result.Success);
        Assert.Contains("данные", result.Message);
    }

    [Fact]
    public async Task DownloadViaLink_InvalidToken_ReturnsNotFound()
    {
        var result = await _storageService.DownloadFileViaLinkAsync("invalid-token");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DownloadViaDirectLink_InvalidToken_ReturnsNotFound()
    {
        var result = await _storageService.DownloadFileViaDirectLinkAsync("invalid-token");

        Assert.False(result.Success);
    }
}

public class StorageTestFixture : IAsyncLifetime
{
    public ServiceProvider ServiceProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Grpc:StorageUrl"] = "https://localhost:8081",
                ["Grpc:SecurityUrl"] = "https://localhost:7203"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddHttpClient();

        services.AddGrpcClient<Contracts.Protos.Storage.StorageService.StorageServiceClient>(options =>
        {
            options.Address = new Uri(configuration["Grpc:StorageUrl"]!);
        });

        services.AddGrpcClient<Contracts.Protos.Security.SecurityService.SecurityServiceClient>(options =>
        {
            options.Address = new Uri(configuration["Grpc:SecurityUrl"]!);
        });

        services.AddSingleton<IJwtTokenProvider, TestJwtTokenProvider>();
        services.AddScoped<IStorageService, StorageClientService>();

        ServiceProvider = services.BuildServiceProvider();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        await Task.CompletedTask;
    }
}

public class TestJwtTokenProvider : IJwtTokenProvider
{
    public Task<string> GetOrRefreshTokenAsync()
    {
        return Task.FromResult("test-jwt-token");
    }

    public Task SetTokenAsync(JwtToken token)
    {
        return Task.CompletedTask;
    }
}
