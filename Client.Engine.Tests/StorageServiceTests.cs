using System.Threading.Tasks;
using Client.Engine.Interfaces;
using Client.Engine.Models;
using Client.Engine.Services;
using Contracts.Protos.Security;
using Contracts.Protos.Storage;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Client.Engine.Tests;

public class StorageServiceTests
{
    private readonly Mock<IJwtTokenProvider> _jwtTokenProviderMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<StorageClientService>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<Contracts.Protos.Storage.StorageService.StorageServiceClient> _storageClientMock;
    private readonly Mock<Contracts.Protos.Security.SecurityService.SecurityServiceClient> _securityClientMock;
    private readonly StorageClientService _service;

    public StorageServiceTests()
    {
        _jwtTokenProviderMock = new Mock<IJwtTokenProvider>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<StorageClientService>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _storageClientMock = new Mock<Contracts.Protos.Storage.StorageService.StorageServiceClient>();
        _securityClientMock = new Mock<Contracts.Protos.Security.SecurityService.SecurityServiceClient>();

        _jwtTokenProviderMock.Setup(x => x.GetOrRefreshTokenAsync()).ReturnsAsync("test-jwt-token");
        _configurationMock.Setup(x => x["Grpc:StorageUrl"]).Returns("https://localhost:8081");

        _service = new StorageClientService(
            _loggerMock.Object,
            _storageClientMock.Object,
            _securityClientMock.Object,
            _jwtTokenProviderMock.Object,
            _configurationMock.Object,
            _httpClientFactoryMock.Object);
    }

    [Fact]
    public async Task UploadFileAsync_EmptyFileName_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _service.UploadFileAsync(stream, "", "docs");

        Assert.False(result.Success);
        Assert.Contains("пустым", result.Message);
    }

    [Fact]
    public async Task UploadFileAsync_FileNameWithSlash_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _service.UploadFileAsync(stream, "file/name.txt", "docs");

        Assert.False(result.Success);
        Assert.Contains("разделителей", result.Message);
    }

    [Fact]
    public async Task UploadFileAsync_InvalidPath_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _service.UploadFileAsync(stream, "file.txt", "../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task MoveFileAsync_InvalidPath_ReturnsFailure()
    {
        var result = await _service.MoveFileAsync(Guid.NewGuid(), "../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task CopyFileAsync_InvalidPath_ReturnsFailure()
    {
        var result = await _service.CopyFileAsync(Guid.NewGuid(), "../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task RenameFileAsync_EmptyName_ReturnsFailure()
    {
        var result = await _service.RenameFileAsync(Guid.NewGuid(), "");

        Assert.False(result.Success);
        Assert.Contains("пустым", result.Message);
    }

    [Fact]
    public async Task RenameFileAsync_NameWithSlash_ReturnsFailure()
    {
        var result = await _service.RenameFileAsync(Guid.NewGuid(), "new/name.txt");

        Assert.False(result.Success);
        Assert.Contains("разделителей", result.Message);
    }

    [Fact]
    public async Task NewDirectoryAsync_EmptyPath_ReturnsFailure()
    {
        var result = await _service.NewDirectoryAsync("");

        Assert.False(result.Success);
        Assert.Contains("пустым", result.Message);
    }

    [Fact]
    public async Task NewDirectoryAsync_TraversalPath_ReturnsFailure()
    {
        var result = await _service.NewDirectoryAsync("../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task RenameDirectoryAsync_EmptyName_ReturnsFailure()
    {
        var result = await _service.RenameDirectoryAsync(Guid.NewGuid(), "");

        Assert.False(result.Success);
        Assert.Contains("пустым", result.Message);
    }

    [Fact]
    public async Task MoveDirectoryAsync_InvalidPath_ReturnsFailure()
    {
        var result = await _service.MoveDirectoryAsync(Guid.NewGuid(), "../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task CopyDirectoryAsync_InvalidPath_ReturnsFailure()
    {
        var result = await _service.CopyDirectoryAsync(Guid.NewGuid(), "../etc");

        Assert.False(result.Success);
        Assert.Contains("Path traversal", result.Message);
    }

    [Fact]
    public async Task UpdateMetadataAsync_ReservedKey_ReturnsFailure()
    {
        var metadata = new Dictionary<string, string> { { "storageKey", "value" } };
        var result = await _service.UpdateMetadataAsync(Guid.NewGuid(), metadata);

        Assert.False(result.Success);
        Assert.Contains("зарезервирован", result.Message);
    }

    [Fact]
    public async Task UpdateMetadataAsync_TooManyKeys_ReturnsFailure()
    {
        var metadata = new Dictionary<string, string>();
        for (int i = 0; i < 65; i++)
        {
            metadata[$"key{i}"] = "value";
        }
        var result = await _service.UpdateMetadataAsync(Guid.NewGuid(), metadata);

        Assert.False(result.Success);
        Assert.Contains("64", result.Message);
    }

    [Fact]
    public async Task GenerateLinkAsync_InvalidTtl_ReturnsFailure()
    {
        var result = await _service.GenerateLinkAsync(Guid.NewGuid(), ttlSeconds: -1);

        Assert.False(result.Success);
        Assert.Contains("положительным", result.Message);
    }

    [Fact]
    public async Task GenerateLinkAsync_TtlTooLarge_ReturnsFailure()
    {
        var result = await _service.GenerateLinkAsync(Guid.NewGuid(), ttlSeconds: 2592001);

        Assert.False(result.Success);
        Assert.Contains("30 дней", result.Message);
    }

    [Fact]
    public async Task GenerateDirectLinkAsync_InvalidTtl_ReturnsFailure()
    {
        var result = await _service.GenerateDirectLinkAsync(Guid.NewGuid(), ttlSeconds: 0);

        Assert.False(result.Success);
        Assert.Contains("положительным", result.Message);
    }

    [Fact]
    public async Task UpdateFileAsync_NegativeOffset_ReturnsFailure()
    {
        var result = await _service.UpdateFileAsync(Guid.NewGuid(), -1, data: new byte[] { 1 });

        Assert.False(result.Success);
        Assert.Contains("offset", result.Message);
    }

    [Fact]
    public async Task UpdateFileAsync_NoDataOrErase_ReturnsFailure()
    {
        var result = await _service.UpdateFileAsync(Guid.NewGuid(), 0);

        Assert.False(result.Success);
        Assert.Contains("данные", result.Message);
    }

    [Fact]
    public async Task DownloadViaLink_InvalidToken_ReturnsFailure()
    {
        var result = await _service.DownloadFileViaLinkAsync("invalid-token");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DownloadViaDirectLink_InvalidToken_ReturnsFailure()
    {
        var result = await _service.DownloadFileViaDirectLinkAsync("invalid-token");

        Assert.False(result.Success);
    }
}
