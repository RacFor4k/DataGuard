using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageFileRepositoryAdditionalTests : IDisposable
{
    private readonly StorageDbContext _db;
    private readonly StorageFileRepository _repo;

    public StorageFileRepositoryAdditionalTests()
    {
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new StorageDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new StorageFileRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<StorageFile> SeedFile(Guid ownerId, string name = "test.txt", string path = "/test.txt")
    {
        var file = new StorageFile
        {
            OwnerId = ownerId,
            FileName = name,
            NormalizedPath = path,
            Size = 100,
            StorageKey = "key-" + Guid.NewGuid().ToString("N"),
            BucketName = "dataguard-storage"
        };
        return await _repo.CreateFileAsync(file);
    }

    // ── GetFileByPathAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetFileByPathAsync_ExistingFile_ReturnsFile()
    {
        var ownerId = Guid.NewGuid();
        await SeedFile(ownerId, "report.txt", "/docs/report.txt");

        var result = await _repo.GetFileByPathAsync(ownerId, "/docs/report.txt");

        result.Should().NotBeNull();
        result!.FileName.Should().Be("report.txt");
        result.NormalizedPath.Should().Be("/docs/report.txt");
    }

    [Fact]
    public async Task GetFileByPathAsync_NotFound_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var result = await _repo.GetFileByPathAsync(ownerId, "/nonexistent.txt");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFileByPathAsync_WrongOwner_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        await SeedFile(ownerId, "secret.txt", "/secret.txt");

        var result = await _repo.GetFileByPathAsync(Guid.NewGuid(), "/secret.txt");

        result.Should().BeNull();
    }

    // ── UpdateFileAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateFileAsync_Success_SetsUpdatedAtUtc()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId, "doc.txt", "/doc.txt");

        file.Size = 999;
        file.UpdatedAtUtc.Should().BeNull();

        var updated = await _repo.UpdateFileAsync(file);

        updated.Should().NotBeNull();
        updated!.UpdatedAtUtc.Should().NotBeNull();
        updated.Size.Should().Be(999);
    }

    [Fact]
    public async Task UpdateFileAsync_FileNotFound_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var file = new StorageFile
        {
            FileId = Guid.NewGuid(),
            OwnerId = ownerId,
            FileName = "ghost.txt",
            NormalizedPath = "/ghost.txt",
            Size = 0,
            StorageKey = "nope",
            BucketName = "dataguard-storage"
        };

        var result = await _repo.UpdateFileAsync(file);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateFileAsync_WrongOwner_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId, "mine.txt", "/mine.txt");

        file.OwnerId = Guid.NewGuid();
        var result = await _repo.UpdateFileAsync(file);

        result.Should().BeNull();
    }

    // ── MoveFileAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveFileAsync_Success_UpdatesPathAndParent()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId, "data.csv", "/old/data.csv");

        var newParentId = Guid.NewGuid();
        var result = await _repo.MoveFileAsync(file.FileId, ownerId, newParentId, "/new/data.csv");

        result.Should().NotBeNull();
        result!.NormalizedPath.Should().Be("/new/data.csv");
        result.ParentDirectoryId.Should().Be(newParentId);
        result.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task MoveFileAsync_FileNotFound_ReturnsNull()
    {
        var result = await _repo.MoveFileAsync(Guid.NewGuid(), Guid.NewGuid(), null, "/nowhere/file.txt");

        result.Should().BeNull();
    }

    [Fact]
    public async Task MoveFileAsync_WrongOwner_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId, "x.txt", "/x.txt");

        var result = await _repo.MoveFileAsync(file.FileId, Guid.NewGuid(), null, "/y/x.txt");

        result.Should().BeNull();
    }

    // ── CopyFileAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CopyFileAsync_Success_NewFileId_SameContent_DifferentStorageKey()
    {
        var ownerId = Guid.NewGuid();
        var source = await SeedFile(ownerId, "orig.txt", "/orig.txt");

        var copy = await _repo.CopyFileAsync(source.FileId, ownerId, null, "/copy.txt");

        copy.Should().NotBeNull();
        copy!.FileId.Should().NotBe(source.FileId);
        copy.StorageKey.Should().Be(source.StorageKey); // CopyFile shares StorageKey initially
        copy.NormalizedPath.Should().Be("/copy.txt");
        copy.Size.Should().Be(source.Size);
    }

    [Fact]
    public async Task CopyFileAsync_SourceNotFound_ReturnsNull()
    {
        var result = await _repo.CopyFileAsync(Guid.NewGuid(), Guid.NewGuid(), null, "/copy.txt");

        result.Should().BeNull();
    }

    // ── RenameFileAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RenameFileAsync_Success_UpdatesPath()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId, "old.txt", "/docs/old.txt");

        var result = await _repo.RenameFileAsync(file.FileId, ownerId, "new.txt");

        result.Should().NotBeNull();
        result!.FileName.Should().Be("new.txt");
        result.NormalizedPath.Should().Be("/docs/new.txt");
        result.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RenameFileAsync_FileNotFound_ReturnsNull()
    {
        var result = await _repo.RenameFileAsync(Guid.NewGuid(), Guid.NewGuid(), "new.txt");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RenameFileAsync_WrongOwner_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId, "mine.txt", "/mine.txt");

        var result = await _repo.RenameFileAsync(file.FileId, Guid.NewGuid(), "yours.txt");

        result.Should().BeNull();
    }

    // ── ListFilesAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListFilesAsync_WithSpecificDirectoryId_ReturnsOnlyThatDirectorysFiles()
    {
        var ownerId = Guid.NewGuid();
        var dirId = Guid.NewGuid();
        var otherDirId = Guid.NewGuid();

        _db.Files.Add(new StorageFile
        {
            OwnerId = ownerId, FileName = "a.txt", NormalizedPath = "/d/a.txt",
            Size = 1, StorageKey = "k1", BucketName = "dataguard-storage", ParentDirectoryId = dirId
        });
        _db.Files.Add(new StorageFile
        {
            OwnerId = ownerId, FileName = "b.txt", NormalizedPath = "/e/b.txt",
            Size = 2, StorageKey = "k2", BucketName = "dataguard-storage", ParentDirectoryId = otherDirId
        });
        await _db.SaveChangesAsync();

        var files = await _repo.ListFilesAsync(ownerId, dirId, recursive: false);

        files.Should().HaveCount(1);
        files[0].FileName.Should().Be("a.txt");
    }

    [Fact]
    public async Task ListFilesAsync_Recursive_IncludesNestedFiles()
    {
        var ownerId = Guid.NewGuid();
        var parentDir = new StorageDirectory
        {
            OwnerId = ownerId, DirectoryName = "docs", NormalizedName = "docs",
            NormalizedPath = "/docs"
        };
        _db.Directories.Add(parentDir);
        await _db.SaveChangesAsync();

        _db.Files.Add(new StorageFile
        {
            OwnerId = ownerId, FileName = "a.txt", NormalizedPath = "/docs/a.txt",
            Size = 1, StorageKey = "k1", BucketName = "dataguard-storage", ParentDirectoryId = parentDir.DirectoryId
        });
        _db.Files.Add(new StorageFile
        {
            OwnerId = ownerId, FileName = "b.txt", NormalizedPath = "/docs/sub/b.txt",
            Size = 2, StorageKey = "k2", BucketName = "dataguard-storage", ParentDirectoryId = Guid.NewGuid()
        });
        await _db.SaveChangesAsync();

        var files = await _repo.ListFilesAsync(ownerId, parentDir.DirectoryId, recursive: true);

        files.Should().HaveCount(2);
        files.Select(f => f.FileName).Should().Contain(["a.txt", "b.txt"]);
    }

    [Fact]
    public async Task ListFilesAsync_FiltersSoftDeletedFiles()
    {
        var ownerId = Guid.NewGuid();

        var active = new StorageFile
        {
            OwnerId = ownerId, FileName = "active.txt", NormalizedPath = "/active.txt",
            Size = 1, StorageKey = "k1", BucketName = "dataguard-storage"
        };
        _db.Files.Add(active);
        await _db.SaveChangesAsync();

        await _repo.DeleteFileAsync(active.FileId, ownerId);

        var files = await _repo.ListFilesAsync(ownerId, null, false);

        files.Should().BeEmpty();
    }
}