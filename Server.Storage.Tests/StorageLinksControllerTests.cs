using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Server.Storage.Controllers;
using Server.Storage.Interfaces;
using Server.Storage.Models;

namespace Server.Storage.Tests;

public class StorageLinksControllerTests
{
    private readonly Mock<IStorageLinkService> _linkService = new();
    private readonly Mock<IStorageBlobStore> _blobStore = new();
    private readonly Mock<IStorageFileRepository> _fileRepo = new();
    private readonly Mock<ILogger<StorageLinksController>> _logger = new();

    private StorageLinksController CreateController()
    {
        return new StorageLinksController(_linkService.Object, _blobStore.Object, _fileRepo.Object, _logger.Object);
    }

    // ── GetLink ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLink_InvalidToken_Returns404()
    {
        var controller = CreateController();
        _linkService.Setup(s => s.GetLinkAsync("bad-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageSharedLink?)null);

        var result = await controller.GetLink("bad-token", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetLink_ValidToken_ReturnsRedirect()
    {
        var controller = CreateController();
        var ownerId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var link = new StorageSharedLink
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            OwnerId = ownerId,
            Token = "valid-token",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            IsDirect = false,
            File = new StorageFile { FileId = fileId, OwnerId = ownerId, FileName = "doc.txt", NormalizedPath = "/doc.txt", StorageKey = "k", BucketName = "b" }
        };

        _linkService.Setup(s => s.GetLinkAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var result = await controller.GetLink("valid-token", CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("/storage/direct/valid-token");
    }

    [Fact]
    public async Task GetLink_ExpiredToken_Returns410()
    {
        var controller = CreateController();
        var ownerId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var link = new StorageSharedLink
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            OwnerId = ownerId,
            Token = "expired",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1),
            IsDirect = false,
            File = new StorageFile { FileId = fileId, OwnerId = ownerId, FileName = "doc.txt", NormalizedPath = "/doc.txt", StorageKey = "k", BucketName = "b" }
        };

        _linkService.Setup(s => s.GetLinkAsync("expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var result = await controller.GetLink("expired", CancellationToken.None);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(410);
    }

    // ── GetDirectLink ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDirectLink_InvalidToken_Returns404()
    {
        var controller = CreateController();
        _linkService.Setup(s => s.GetLinkAsync("bad-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageSharedLink?)null);

        var result = await controller.GetDirectLink("bad-token", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDirectLink_ValidToken_ReturnsFileStream()
    {
        var controller = CreateController();
        var ownerId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var file = new StorageFile
        {
            FileId = fileId, OwnerId = ownerId, FileName = "report.pdf",
            NormalizedPath = "/report.pdf", Size = 1024,
            StorageKey = "obj-key", BucketName = "dataguard-storage"
        };
        var link = new StorageSharedLink
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            OwnerId = ownerId,
            Token = "direct-token",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            IsDirect = true,
            File = file
        };
        var stream = new MemoryStream(new byte[64]);

        _linkService.Setup(s => s.GetLinkAsync("direct-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
        _fileRepo.Setup(r => r.GetFileAsync(fileId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _blobStore.Setup(b => b.GetObjectAsync("dataguard-storage", "obj-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        var result = await controller.GetDirectLink("direct-token", CancellationToken.None);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.FileDownloadName.Should().Be("report.pdf");
        fileResult.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task GetDirectLink_FileNotFound_Returns404()
    {
        var controller = CreateController();
        var ownerId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var link = new StorageSharedLink
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            OwnerId = ownerId,
            Token = "token-no-file",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            IsDirect = true,
            File = new StorageFile { FileId = fileId, OwnerId = ownerId, FileName = "gone.txt", NormalizedPath = "/gone.txt", StorageKey = "k", BucketName = "b" }
        };

        _linkService.Setup(s => s.GetLinkAsync("token-no-file", It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
        _fileRepo.Setup(r => r.GetFileAsync(fileId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFile?)null);

        var result = await controller.GetDirectLink("token-no-file", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}