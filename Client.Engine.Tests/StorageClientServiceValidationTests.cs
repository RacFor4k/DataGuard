using System.IO;
using System.Threading.Tasks;
using Client.Engine.Interfaces;
using Client.Engine.Services;
using Contracts.Protos.Security;
using Contracts.Protos.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Client.Engine.Tests;

public class StorageClientServiceValidationTests
{
    private readonly Mock<IJwtTokenProvider> _jwtTokenProviderMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<StorageClientService>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<StorageService.StorageServiceClient> _storageClientMock;
    private readonly Mock<SecurityService.SecurityServiceClient> _securityClientMock;
    private readonly StorageClientService _service;

    public StorageClientServiceValidationTests()
    {
        _jwtTokenProviderMock = new Mock<IJwtTokenProvider>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<StorageClientService>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _storageClientMock = new Mock<StorageService.StorageServiceClient>();
        _securityClientMock = new Mock<SecurityService.SecurityServiceClient>();

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

    // ── UploadFileAsync: additional validation paths ───────────────────────

    [Fact]
    public async Task UploadFileAsync_NullFileName_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _service.UploadFileAsync(stream, null!, "docs");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UploadFileAsync_FileNameWithBackslash_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _service.UploadFileAsync(stream, "file\\name.txt", "docs");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("разделителей");
    }

    [Fact]
    public async Task UploadFileAsync_FileNameTooLong_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var longName = new string('a', 1025);
        var result = await _service.UploadFileAsync(stream, longName, "docs");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("1024");
    }

    [Fact]
    public async Task UploadFileAsync_AbsolutePath_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _service.UploadFileAsync(stream, "file.txt", "/absolute/path");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("относительным");
    }

    [Fact]
    public async Task UploadFileAsync_PathWithDriveLetter_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _service.UploadFileAsync(stream, "file.txt", "C:docs");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("буквы дисков");
    }

    [Fact]
    public async Task UploadFileAsync_PathTooLong_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var longPath = new string('a', 4097);
        var result = await _service.UploadFileAsync(stream, "file.txt", longPath);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("4096");
    }

    [Fact]
    public async Task UploadFileAsync_EmptyStream_SucceedsWithValidationPassing()
    {
        // An empty stream passes validation (validation checks fileName/path, not stream content)
        // but will fail later when trying to read from stream. The validation itself should pass.
        using var emptyStream = new MemoryStream();
        var result = await _service.UploadFileAsync(emptyStream, "file.txt", "docs");

        // Validation passes, but the service may fail at gRPC or stream read level
        // The key assertion: the result is returned (no unhandled exception) and
        // validation-specific error messages are NOT present
        result.Message.Should().NotContain("разделителей");
        result.Message.Should().NotContain("пустым");
    }

    // ── UpdateFileAsync: negative eraseSize ────────────────────────────────

    [Fact]
    public async Task UpdateFileAsync_NegativeEraseSize_ReturnsFailure()
    {
        var result = await _service.UpdateFileAsync(Guid.NewGuid(), 0, eraseSize: -5);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("offset");
    }

    // ── MoveFileAsync: additional validation paths ─────────────────────────

    [Fact]
    public async Task MoveFileAsync_EmptyPath_ReturnsFailure()
    {
        var result = await _service.MoveFileAsync(Guid.NewGuid(), "");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task MoveFileAsync_WhitespacePath_ReturnsFailure()
    {
        var result = await _service.MoveFileAsync(Guid.NewGuid(), "   ");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task MoveFileAsync_AbsolutePath_ReturnsFailure()
    {
        var result = await _service.MoveFileAsync(Guid.NewGuid(), "/root/path");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("относительным");
    }

    [Fact]
    public async Task MoveFileAsync_DriveLetterPath_ReturnsFailure()
    {
        var result = await _service.MoveFileAsync(Guid.NewGuid(), "D:secret");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("буквы дисков");
    }

    [Fact]
    public async Task MoveFileAsync_TooLongPath_ReturnsFailure()
    {
        var longPath = new string('x', 4097);
        var result = await _service.MoveFileAsync(Guid.NewGuid(), longPath);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("4096");
    }

    // ── CopyFileAsync: additional validation paths ─────────────────────────

    [Fact]
    public async Task CopyFileAsync_EmptyPath_ReturnsFailure()
    {
        var result = await _service.CopyFileAsync(Guid.NewGuid(), "");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task CopyFileAsync_WhitespacePath_ReturnsFailure()
    {
        var result = await _service.CopyFileAsync(Guid.NewGuid(), "   ");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task CopyFileAsync_AbsolutePath_ReturnsFailure()
    {
        var result = await _service.CopyFileAsync(Guid.NewGuid(), "/etc/passwd");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("относительным");
    }

    [Fact]
    public async Task CopyFileAsync_DriveLetterPath_ReturnsFailure()
    {
        var result = await _service.CopyFileAsync(Guid.NewGuid(), "E:data");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("буквы дисков");
    }

    [Fact]
    public async Task CopyFileAsync_TooLongPath_ReturnsFailure()
    {
        var longPath = new string('y', 4097);
        var result = await _service.CopyFileAsync(Guid.NewGuid(), longPath);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("4096");
    }

    // ── RenameFileAsync: additional validation paths ───────────────────────

    [Fact]
    public async Task RenameFileAsync_WhitespaceName_ReturnsFailure()
    {
        var result = await _service.RenameFileAsync(Guid.NewGuid(), "   ");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task RenameFileAsync_BackslashInName_ReturnsFailure()
    {
        var result = await _service.RenameFileAsync(Guid.NewGuid(), "file\\name.txt");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("разделителей");
    }

    [Fact]
    public async Task RenameFileAsync_TooLongName_ReturnsFailure()
    {
        var longName = new string('z', 1025);
        var result = await _service.RenameFileAsync(Guid.NewGuid(), longName);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("1024");
    }

    // ── NewDirectoryAsync: additional validation paths ─────────────────────

    [Fact]
    public async Task NewDirectoryAsync_WhitespacePath_ReturnsFailure()
    {
        var result = await _service.NewDirectoryAsync("   ");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task NewDirectoryAsync_AbsolutePath_ReturnsFailure()
    {
        var result = await _service.NewDirectoryAsync("/root");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("относительным");
    }

    [Fact]
    public async Task NewDirectoryAsync_DriveLetterPath_ReturnsFailure()
    {
        var result = await _service.NewDirectoryAsync("C:Windows");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("буквы дисков");
    }

    [Fact]
    public async Task NewDirectoryAsync_TooLongPath_ReturnsFailure()
    {
        var longPath = new string('p', 4097);
        var result = await _service.NewDirectoryAsync(longPath);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("4096");
    }

    // ── RenameDirectoryAsync: additional validation paths ──────────────────

    [Fact]
    public async Task RenameDirectoryAsync_WhitespaceName_ReturnsFailure()
    {
        var result = await _service.RenameDirectoryAsync(Guid.NewGuid(), "   ");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task RenameDirectoryAsync_SlashInName_ReturnsFailure()
    {
        var result = await _service.RenameDirectoryAsync(Guid.NewGuid(), "dir/name");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("разделителей");
    }

    [Fact]
    public async Task RenameDirectoryAsync_BackslashInName_ReturnsFailure()
    {
        var result = await _service.RenameDirectoryAsync(Guid.NewGuid(), "dir\\name");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("разделителей");
    }

    // ── MoveDirectoryAsync: additional validation paths ────────────────────

    [Fact]
    public async Task MoveDirectoryAsync_EmptyPath_ReturnsFailure()
    {
        var result = await _service.MoveDirectoryAsync(Guid.NewGuid(), "");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task MoveDirectoryAsync_AbsolutePath_ReturnsFailure()
    {
        var result = await _service.MoveDirectoryAsync(Guid.NewGuid(), "/etc");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("относительным");
    }

    // ── CopyDirectoryAsync: additional validation paths ────────────────────

    [Fact]
    public async Task CopyDirectoryAsync_EmptyPath_ReturnsFailure()
    {
        var result = await _service.CopyDirectoryAsync(Guid.NewGuid(), "");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("пустым");
    }

    [Fact]
    public async Task CopyDirectoryAsync_AbsolutePath_ReturnsFailure()
    {
        var result = await _service.CopyDirectoryAsync(Guid.NewGuid(), "/etc");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("относительным");
    }
}