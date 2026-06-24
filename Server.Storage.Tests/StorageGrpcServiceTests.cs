using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Server.Storage.Interfaces;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

// Minimal concrete ServerCallContext for testing (Grpc.Core 2.x uses *Core-suffixed abstract members)
public class TestGrpcCallContext : ServerCallContext
{
    private readonly IDictionary<object, object> _userState;

    public TestGrpcCallContext(HttpContext? httpContext = null)
    {
        _userState = new Dictionary<object, object>();
        if (httpContext != null)
            _userState["__HttpContext"] = httpContext;
    }

    protected override IDictionary<object, object> UserStateCore => _userState;
    protected override string MethodCore => "/StorageService/Test";
    protected override string HostCore => "localhost";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(5);
    protected override Metadata RequestHeadersCore => new();
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override string PeerCore => "ipv4:127.0.0.1:1234";
    protected override AuthContext AuthContextCore => null!;
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => null!;
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override Status StatusCore { get; set; }
    protected override Metadata ResponseTrailersCore => new();
}

public class StorageGrpcServiceTests
{
    private readonly Mock<IStorageFileRepository> _fileRepo = new();
    private readonly Mock<IStorageDirectoryRepository> _dirRepo = new();
    private readonly Mock<IStorageBlobStore> _blobStore = new();
    private readonly Mock<IStorageNonceService> _nonceService = new();
    private readonly Mock<IStoragePathValidator> _pathValidator = new();
    private readonly Mock<IStorageMetadataService> _metadataService = new();
    private readonly Mock<IStorageLinkService> _linkService = new();
    private readonly Mock<IOwnerIdentityProvider> _ownerProvider = new();
    private readonly Mock<ILogger<StorageGrpcService>> _logger = new();

    private static readonly Guid TestOwnerId = Guid.NewGuid();

    private StorageGrpcService CreateService()
    {
        return new StorageGrpcService(
            _fileRepo.Object,
            _dirRepo.Object,
            _blobStore.Object,
            _nonceService.Object,
            _pathValidator.Object,
            _metadataService.Object,
            _linkService.Object,
            _ownerProvider.Object,
            _logger.Object
        );
    }

    private ServerCallContext CreateContext(Guid? ownerId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (ownerId.HasValue)
        {
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim("sub", ownerId.Value.ToString()) }
                )
            );
        }
        _ownerProvider.Setup(p => p.GetOwnerId(httpContext)).Returns(ownerId);
        return new TestGrpcCallContext(httpContext);
    }

    private ServerCallContext AuthedContext() => CreateContext(TestOwnerId);

    // ═══════════════════════════════════════════════════════════════════════
    // DeleteFile
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteFile_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null); // no owner

        var result = await service.DeleteFile(new Contracts.Protos.Storage.DeleteFileRequest { FileId = Guid.NewGuid().ToString() }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFile_InvalidFileId_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        var result = await service.DeleteFile(new Contracts.Protos.Storage.DeleteFileRequest { FileId = "not-a-guid", NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFile_EmptyNonce_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        var result = await service.DeleteFile(new Contracts.Protos.Storage.DeleteFileRequest { FileId = Guid.NewGuid().ToString() }, ctx);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("nonce");
    }

    [Fact]
    public async Task DeleteFile_InvalidNonce_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        _nonceService.Setup(n => n.TryConsumeNonceAsync(It.IsAny<Guid>(), "DeleteFile", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await service.DeleteFile(new Contracts.Protos.Storage.DeleteFileRequest { FileId = Guid.NewGuid().ToString(), NonceToken = "bad" }, ctx);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("nonce");
    }

    [Fact]
    public async Task DeleteFile_FileNotFound_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "DeleteFile", "nonce", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _fileRepo.Setup(r => r.GetFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFile?)null);

        var result = await service.DeleteFile(new Contracts.Protos.Storage.DeleteFileRequest { FileId = fileId.ToString(), NonceToken = "nonce" }, ctx);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("не найден");
    }

    [Fact]
    public async Task DeleteFile_Success_ReturnsTrue()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();
        var file = new StorageFile { FileId = fileId, OwnerId = TestOwnerId, FileName = "x.txt", NormalizedPath = "/x.txt", StorageKey = "k", BucketName = "b", Size = 1 };

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "DeleteFile", "nonce", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _fileRepo.Setup(r => r.GetFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _fileRepo.Setup(r => r.DeleteFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.DeleteFile(new Contracts.Protos.Storage.DeleteFileRequest { FileId = fileId.ToString(), NonceToken = "nonce" }, ctx);

        result.Success.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RenameFile
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RenameFile_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null);

        var result = await service.RenameFile(new Contracts.Protos.Storage.RenameFileRequest { FileId = Guid.NewGuid().ToString(), NewName = "x", NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RenameFile_EmptyName_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "RenameFile", "n", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.RenameFile(new Contracts.Protos.Storage.RenameFileRequest { FileId = fileId.ToString(), NewName = "", NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RenameFile_InvalidNonce_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        _nonceService.Setup(n => n.TryConsumeNonceAsync(It.IsAny<Guid>(), "RenameFile", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await service.RenameFile(new Contracts.Protos.Storage.RenameFileRequest { FileId = Guid.NewGuid().ToString(), NewName = "x", NonceToken = "bad" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RenameFile_Success_ReturnsTrue()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();
        var file = new StorageFile { FileId = fileId, OwnerId = TestOwnerId, FileName = "old.txt", NormalizedPath = "/docs/old.txt", StorageKey = "k", BucketName = "b", Size = 1 };

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "RenameFile", "nonce", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _fileRepo.Setup(r => r.GetFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _pathValidator.Setup(p => p.GetParentPath("/docs/old.txt")).Returns("/docs");
        _fileRepo.Setup(r => r.GetFileByPathAsync(TestOwnerId, "/docs/new.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFile?)null);
        _fileRepo.Setup(r => r.RenameFileAsync(fileId, TestOwnerId, "new.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var result = await service.RenameFile(new Contracts.Protos.Storage.RenameFileRequest { FileId = fileId.ToString(), NewName = "new.txt", NonceToken = "nonce" }, ctx);

        result.Success.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NewDirectory
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NewDirectory_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null);

        var result = await service.NewDirectory(new Contracts.Protos.Storage.NewDirectoryRequest { DirectoryPath = "/docs" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task NewDirectory_EmptyPath_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        var result = await service.NewDirectory(new Contracts.Protos.Storage.NewDirectoryRequest { DirectoryPath = "" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task NewDirectory_InvalidPath_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        string ignoredPath = "";
        string ignoredError = "";
        _pathValidator.Setup(p => p.TryNormalizePath("bad/path", out ignoredPath, out ignoredError))
            .Returns(false);

        var result = await service.NewDirectory(new Contracts.Protos.Storage.NewDirectoryRequest { DirectoryPath = "bad/path" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task NewDirectory_Success_ReturnsDirectoryId()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        string outPath = "/docs";
        string outError = "";
        _pathValidator.Setup(p => p.TryNormalizePath("docs", out outPath, out outError))
            .Returns(true);
        _dirRepo.Setup(r => r.GetDirectoryByPathAsync(TestOwnerId, "/docs", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageDirectory?)null);
        _fileRepo.Setup(r => r.GetFileByPathAsync(TestOwnerId, "/docs", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFile?)null);
        _pathValidator.Setup(p => p.GetFileName("/docs")).Returns("docs");
        _pathValidator.Setup(p => p.GetParentPath("/docs")).Returns("/");
        _dirRepo.Setup(r => r.CreateDirectoryAsync(It.IsAny<StorageDirectory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageDirectory d, CancellationToken ct) => d);

        var result = await service.NewDirectory(new Contracts.Protos.Storage.NewDirectoryRequest { DirectoryPath = "docs" }, ctx);

        result.Success.Should().BeTrue();
        result.DirectoryId.Should().NotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DeleteDirectory
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteDirectory_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null);

        var result = await service.DeleteDirectory(new Contracts.Protos.Storage.DeleteDirectoryRequest { DirectoryId = Guid.NewGuid().ToString(), NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDirectory_InvalidDirectoryId_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        var result = await service.DeleteDirectory(new Contracts.Protos.Storage.DeleteDirectoryRequest { DirectoryId = "invalid", NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDirectory_EmptyNonce_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        var result = await service.DeleteDirectory(new Contracts.Protos.Storage.DeleteDirectoryRequest { DirectoryId = Guid.NewGuid().ToString() }, ctx);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("nonce");
    }

    [Fact]
    public async Task DeleteDirectory_NonRecursiveWithChildren_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var dirId = Guid.NewGuid();
        var dir = new StorageDirectory { DirectoryId = dirId, OwnerId = TestOwnerId, DirectoryName = "docs", NormalizedPath = "/docs" };

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "DeleteDirectory", "n", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _dirRepo.Setup(r => r.GetDirectoryAsync(dirId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dir);
        _dirRepo.Setup(r => r.DeleteDirectoryAsync(dirId, TestOwnerId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await service.DeleteDirectory(new Contracts.Protos.Storage.DeleteDirectoryRequest { DirectoryId = dirId.ToString(), NonceToken = "n", Recursive = false }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDirectory_Success_ReturnsTrue()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var dirId = Guid.NewGuid();
        var dir = new StorageDirectory { DirectoryId = dirId, OwnerId = TestOwnerId, DirectoryName = "docs", NormalizedPath = "/docs" };

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "DeleteDirectory", "n", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _dirRepo.Setup(r => r.GetDirectoryAsync(dirId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dir);
        _dirRepo.Setup(r => r.DeleteDirectoryAsync(dirId, TestOwnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.DeleteDirectory(new Contracts.Protos.Storage.DeleteDirectoryRequest { DirectoryId = dirId.ToString(), NonceToken = "n", Recursive = true }, ctx);

        result.Success.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MoveFile
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MoveFile_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null);

        var result = await service.MoveFile(new Contracts.Protos.Storage.MoveFileRequest { FileId = Guid.NewGuid().ToString(), NewPath = "/dest", NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task MoveFile_EmptyNewPath_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "MoveFile", "n", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.MoveFile(new Contracts.Protos.Storage.MoveFileRequest { FileId = fileId.ToString(), NewPath = "", NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task MoveFile_InvalidNonce_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        _nonceService.Setup(n => n.TryConsumeNonceAsync(It.IsAny<Guid>(), "MoveFile", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await service.MoveFile(new Contracts.Protos.Storage.MoveFileRequest { FileId = Guid.NewGuid().ToString(), NewPath = "/dest", NonceToken = "bad" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task MoveFile_Success_ReturnsTrue()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();
        var file = new StorageFile { FileId = fileId, OwnerId = TestOwnerId, FileName = "data.csv", NormalizedPath = "/old/data.csv", StorageKey = "k", BucketName = "b", Size = 1 };
        var targetDir = new StorageDirectory { DirectoryId = Guid.NewGuid(), OwnerId = TestOwnerId, DirectoryName = "new", NormalizedPath = "/new" };

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "MoveFile", "n", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        string movePath = "/new";
        string moveErr = "";
        _pathValidator.Setup(p => p.TryNormalizePath("new", out movePath, out moveErr)).Returns(true);
        _fileRepo.Setup(r => r.GetFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _fileRepo.Setup(r => r.GetFileByPathAsync(TestOwnerId, "/new/data.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFile?)null);
        _dirRepo.Setup(r => r.GetDirectoryByPathAsync(TestOwnerId, "/new", It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetDir);
        _fileRepo.Setup(r => r.MoveFileAsync(fileId, TestOwnerId, targetDir.DirectoryId, "/new/data.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var result = await service.MoveFile(new Contracts.Protos.Storage.MoveFileRequest { FileId = fileId.ToString(), NewPath = "new", NonceToken = "n" }, ctx);

        result.Success.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CopyFile
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CopyFile_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null);

        var result = await service.CopyFile(new Contracts.Protos.Storage.CopyFileRequest { FileId = Guid.NewGuid().ToString(), NewPath = "/dest", NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CopyFile_EmptyNewPath_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "CopyFile", "n", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.CopyFile(new Contracts.Protos.Storage.CopyFileRequest { FileId = fileId.ToString(), NewPath = "", NonceToken = "n" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CopyFile_InvalidNonce_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        _nonceService.Setup(n => n.TryConsumeNonceAsync(It.IsAny<Guid>(), "CopyFile", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await service.CopyFile(new Contracts.Protos.Storage.CopyFileRequest { FileId = Guid.NewGuid().ToString(), NewPath = "/dest", NonceToken = "bad" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CopyFile_Success_ReturnsNewFileId()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();
        var newFileId = Guid.NewGuid();
        var file = new StorageFile { FileId = fileId, OwnerId = TestOwnerId, FileName = "data.csv", NormalizedPath = "/orig/data.csv", StorageKey = "old-key", BucketName = "b", Size = 10, ContentHash = new byte[32] };
        var copy = new StorageFile { FileId = newFileId, OwnerId = TestOwnerId, FileName = "data.csv", NormalizedPath = "/new/data.csv", StorageKey = "old-key", BucketName = "b", Size = 10 };
        var targetDir = new StorageDirectory { DirectoryId = Guid.NewGuid(), OwnerId = TestOwnerId, DirectoryName = "new", NormalizedPath = "/new" };

        _nonceService.Setup(n => n.TryConsumeNonceAsync(TestOwnerId, "CopyFile", "n", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        string copyPath = "/new";
        string copyErr = "";
        _pathValidator.Setup(p => p.TryNormalizePath("new", out copyPath, out copyErr)).Returns(true);
        _fileRepo.Setup(r => r.GetFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _fileRepo.Setup(r => r.GetFileByPathAsync(TestOwnerId, "/new/data.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFile?)null);
        _dirRepo.Setup(r => r.GetDirectoryByPathAsync(TestOwnerId, "/new", It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetDir);
        _blobStore.Setup(b => b.GenerateStorageKey(".csv")).Returns("new-storage-key");
        _blobStore.Setup(b => b.CopyObjectAsync("b", "old-key", "dataguard-storage", "new-storage-key", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fileRepo.Setup(r => r.CopyFileAsync(fileId, TestOwnerId, targetDir.DirectoryId, "/new/data.csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(copy);
        _fileRepo.Setup(r => r.UpdateFileAsync(copy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(copy);

        var result = await service.CopyFile(new Contracts.Protos.Storage.CopyFileRequest { FileId = fileId.ToString(), NewPath = "new", NonceToken = "n" }, ctx);

        result.Success.Should().BeTrue();
        result.NewFileId.Should().Be(newFileId.ToString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetMetadata
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMetadata_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null);

        var result = await service.GetMetadata(new Contracts.Protos.Storage.GetMetadataRequest { FileId = Guid.NewGuid().ToString() }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetMetadata_InvalidFileId_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        var result = await service.GetMetadata(new Contracts.Protos.Storage.GetMetadataRequest { FileId = "invalid" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetMetadata_Success_ReturnsMetadata()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();
        var file = new StorageFile { FileId = fileId, OwnerId = TestOwnerId, FileName = "doc.txt", NormalizedPath = "/docs/doc.txt", Size = 42, StorageKey = "k", BucketName = "b" };

        _fileRepo.Setup(r => r.GetFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _metadataService.Setup(m => m.GetMetadataAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["author"] = "test" });
        _pathValidator.Setup(p => p.GetParentPath("/docs/doc.txt")).Returns("/docs");

        var result = await service.GetMetadata(new Contracts.Protos.Storage.GetMetadataRequest { FileId = fileId.ToString() }, ctx);

        result.Success.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata.FileName.Should().Be("doc.txt");
        result.Metadata.Size.Should().Be(42);
        result.Metadata.Metadata["author"].Should().Be("test");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UpdateMetadata
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateMetadata_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null);

        var result = await service.UpdateMetadata(new Contracts.Protos.Storage.UpdateMetadataRequest { FileId = Guid.NewGuid().ToString() }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMetadata_InvalidFileId_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        var result = await service.UpdateMetadata(new Contracts.Protos.Storage.UpdateMetadataRequest { FileId = "bad" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMetadata_FileNotFound_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();

        _fileRepo.Setup(r => r.GetFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFile?)null);

        var result = await service.UpdateMetadata(new Contracts.Protos.Storage.UpdateMetadataRequest { FileId = fileId.ToString() }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMetadata_Success_ReturnsTrue()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var fileId = Guid.NewGuid();
        var file = new StorageFile { FileId = fileId, OwnerId = TestOwnerId, FileName = "x.txt", NormalizedPath = "/x.txt", Size = 1, StorageKey = "k", BucketName = "b" };

        _fileRepo.Setup(r => r.GetFileAsync(fileId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _metadataService.Setup(m => m.UpdateMetadataAsync(fileId, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new Contracts.Protos.Storage.UpdateMetadataRequest { FileId = fileId.ToString() };
        request.Metadata["key1"] = "value1";

        var result = await service.UpdateMetadata(request, ctx);

        result.Success.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ListDirectory
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListDirectory_NoOwner_ReturnsFail()
    {
        var service = CreateService();
        var ctx = CreateContext(null);

        var result = await service.ListDirectory(new Contracts.Protos.Storage.ListDirectoryRequest { DirectoryId = Guid.NewGuid().ToString() }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ListDirectory_InvalidDirectoryId_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();

        var result = await service.ListDirectory(new Contracts.Protos.Storage.ListDirectoryRequest { DirectoryId = "invalid" }, ctx);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ListDirectory_Success_ReturnsItems()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var dirId = Guid.NewGuid();
        var dir = new StorageDirectory { DirectoryId = dirId, OwnerId = TestOwnerId, DirectoryName = "docs", NormalizedPath = "/docs" };
        var file = new StorageFile { FileId = Guid.NewGuid(), OwnerId = TestOwnerId, FileName = "report.txt", NormalizedPath = "/docs/report.txt", Size = 50, StorageKey = "k", BucketName = "b" };

        _dirRepo.Setup(r => r.GetDirectoryAsync(dirId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dir);
        _pathValidator.Setup(p => p.GetParentPath(It.IsAny<string>())).Returns("/docs");
        _dirRepo.Setup(r => r.ListDirectoryContentsAsync(TestOwnerId, dirId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<object> { file });

        var result = await service.ListDirectory(new Contracts.Protos.Storage.ListDirectoryRequest { DirectoryId = dirId.ToString() }, ctx);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].FileName.Should().Be("report.txt");
    }

    [Fact]
    public async Task ListDirectory_DirectoryNotFound_ReturnsFail()
    {
        var service = CreateService();
        var ctx = AuthedContext();
        var dirId = Guid.NewGuid();

        _dirRepo.Setup(r => r.GetDirectoryAsync(dirId, TestOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageDirectory?)null);

        var result = await service.ListDirectory(new Contracts.Protos.Storage.ListDirectoryRequest { DirectoryId = dirId.ToString() }, ctx);

        result.Success.Should().BeFalse();
    }
}