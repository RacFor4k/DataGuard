using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageDirectoryRepositoryAdditionalTests : IDisposable
{
    private readonly StorageDbContext _db;
    private readonly StorageDirectoryRepository _repo;

    public StorageDirectoryRepositoryAdditionalTests()
    {
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new StorageDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new StorageDirectoryRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<StorageDirectory> SeedDir(Guid ownerId, string name, string path, Guid? parentId = null)
    {
        var dir = new StorageDirectory
        {
            OwnerId = ownerId,
            DirectoryName = name,
            NormalizedName = name.ToLowerInvariant(),
            NormalizedPath = path,
            ParentDirectoryId = parentId
        };
        return await _repo.CreateDirectoryAsync(dir);
    }

    // ── GetDirectoryByPathAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetDirectoryByPathAsync_ExistingDirectory_ReturnsDirectory()
    {
        var ownerId = Guid.NewGuid();
        await SeedDir(ownerId, "docs", "/docs");

        var result = await _repo.GetDirectoryByPathAsync(ownerId, "/docs");

        result.Should().NotBeNull();
        result!.DirectoryName.Should().Be("docs");
    }

    [Fact]
    public async Task GetDirectoryByPathAsync_NotFound_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var result = await _repo.GetDirectoryByPathAsync(ownerId, "/nonexistent");

        result.Should().BeNull();
    }

    // ── DeleteDirectoryAsync edge cases ──────────────────────────────────────

    [Fact]
    public async Task DeleteDirectoryAsync_NotFound_ReturnsFalse()
    {
        var result = await _repo.DeleteDirectoryAsync(Guid.NewGuid(), Guid.NewGuid(), recursive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDirectoryAsync_NonRecursiveWithChildFiles_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var dir = await SeedDir(ownerId, "docs", "/docs");

        _db.Files.Add(new StorageFile
        {
            OwnerId = ownerId, FileName = "file.txt", NormalizedPath = "/docs/file.txt",
            Size = 10, StorageKey = "k1", BucketName = "dataguard-storage",
            ParentDirectoryId = dir.DirectoryId
        });
        await _db.SaveChangesAsync();

        var result = await _repo.DeleteDirectoryAsync(dir.DirectoryId, ownerId, recursive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDirectoryAsync_NonRecursiveWithSubdirs_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var dir = await SeedDir(ownerId, "docs", "/docs");

        await SeedDir(ownerId, "sub", "/docs/sub", dir.DirectoryId);

        var result = await _repo.DeleteDirectoryAsync(dir.DirectoryId, ownerId, recursive: false);

        result.Should().BeFalse();
    }

    // ── RenameDirectoryAsync edge cases ──────────────────────────────────────

    [Fact]
    public async Task RenameDirectoryAsync_NotFound_ReturnsNull()
    {
        var result = await _repo.RenameDirectoryAsync(Guid.NewGuid(), Guid.NewGuid(), "newname");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RenameDirectoryAsync_RootLevelRename_UpdatesPath()
    {
        var ownerId = Guid.NewGuid();
        var dir = await SeedDir(ownerId, "old", "/old");

        var result = await _repo.RenameDirectoryAsync(dir.DirectoryId, ownerId, "newname");

        result.Should().NotBeNull();
        result!.DirectoryName.Should().Be("newname");
        result.NormalizedPath.Should().Be("/newname");
        result.UpdatedAtUtc.Should().NotBeNull();
    }

    // ── MoveDirectoryAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task MoveDirectoryAsync_Success_UpdatesPathAndParent()
    {
        var ownerId = Guid.NewGuid();
        var dir = await SeedDir(ownerId, "src", "/src");

        var newParent = await SeedDir(ownerId, "dest", "/dest");

        var result = await _repo.MoveDirectoryAsync(dir.DirectoryId, ownerId, newParent.DirectoryId, "/dest/src");

        result.Should().NotBeNull();
        result!.NormalizedPath.Should().Be("/dest/src");
        result.ParentDirectoryId.Should().Be(newParent.DirectoryId);
        result.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task MoveDirectoryAsync_NestedPathsUpdated()
    {
        var ownerId = Guid.NewGuid();
        var dir = await SeedDir(ownerId, "folder", "/folder");
        var sub = await SeedDir(ownerId, "sub", "/folder/sub", dir.DirectoryId);

        var file = new StorageFile
        {
            OwnerId = ownerId, FileName = "f.txt", NormalizedPath = "/folder/sub/f.txt",
            Size = 1, StorageKey = "k1", BucketName = "dataguard-storage",
            ParentDirectoryId = sub.DirectoryId
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        await _repo.MoveDirectoryAsync(dir.DirectoryId, ownerId, null, "/moved");

        var updatedSub = await _db.Directories.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.DirectoryId == sub.DirectoryId);
        updatedSub.Should().NotBeNull();
        updatedSub!.NormalizedPath.Should().Be("/moved/sub");

        var updatedFile = await _db.Files.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.FileId == file.FileId);
        updatedFile.Should().NotBeNull();
        updatedFile!.NormalizedPath.Should().Be("/moved/sub/f.txt");
    }

    // ── GetSubdirectoriesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetSubdirectoriesAsync_WithParent_ReturnsChildren()
    {
        var ownerId = Guid.NewGuid();
        var parent = await SeedDir(ownerId, "root", "/root");
        await SeedDir(ownerId, "a", "/root/a", parent.DirectoryId);
        await SeedDir(ownerId, "b", "/root/b", parent.DirectoryId);

        var subs = await _repo.GetSubdirectoriesAsync(ownerId, parent.DirectoryId);

        subs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSubdirectoriesAsync_NullParent_ReturnsRootDirectories()
    {
        var ownerId = Guid.NewGuid();
        await SeedDir(ownerId, "root1", "/root1");
        await SeedDir(ownerId, "root2", "/root2");

        var subs = await _repo.GetSubdirectoriesAsync(ownerId, null);

        subs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSubdirectoriesAsync_MultipleLevels_ReturnsOnlyDirectChildren()
    {
        var ownerId = Guid.NewGuid();
        var root = await SeedDir(ownerId, "root", "/root");
        var child = await SeedDir(ownerId, "child", "/root/child", root.DirectoryId);
        await SeedDir(ownerId, "grandchild", "/root/child/grandchild", child.DirectoryId);

        var subs = await _repo.GetSubdirectoriesAsync(ownerId, root.DirectoryId);

        subs.Should().HaveCount(1);
        subs[0].DirectoryName.Should().Be("child");
    }

    // ── ListDirectoryContentsAsync ───────────────────────────────────────────

    [Fact]
    public async Task ListDirectoryContentsAsync_NonRecursive_ReturnsFilesAndDirs()
    {
        var ownerId = Guid.NewGuid();
        var dir = await SeedDir(ownerId, "docs", "/docs");

        _db.Files.Add(new StorageFile
        {
            OwnerId = ownerId, FileName = "a.txt", NormalizedPath = "/docs/a.txt",
            Size = 1, StorageKey = "k1", BucketName = "dataguard-storage",
            ParentDirectoryId = dir.DirectoryId
        });
        await _db.SaveChangesAsync();

        await SeedDir(ownerId, "sub", "/docs/sub", dir.DirectoryId);

        var contents = await _repo.ListDirectoryContentsAsync(ownerId, dir.DirectoryId, recursive: false);

        contents.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListDirectoryContentsAsync_Recursive_ReturnsFullTree()
    {
        var ownerId = Guid.NewGuid();
        var dir = await SeedDir(ownerId, "root", "/root");
        var sub = await SeedDir(ownerId, "sub", "/root/sub", dir.DirectoryId);

        _db.Files.Add(new StorageFile
        {
            OwnerId = ownerId, FileName = "f1.txt", NormalizedPath = "/root/f1.txt",
            Size = 1, StorageKey = "k1", BucketName = "dataguard-storage",
            ParentDirectoryId = dir.DirectoryId
        });
        _db.Files.Add(new StorageFile
        {
            OwnerId = ownerId, FileName = "f2.txt", NormalizedPath = "/root/sub/f2.txt",
            Size = 2, StorageKey = "k2", BucketName = "dataguard-storage",
            ParentDirectoryId = sub.DirectoryId
        });
        await _db.SaveChangesAsync();

        var contents = await _repo.ListDirectoryContentsAsync(ownerId, dir.DirectoryId, recursive: true);

        contents.Should().HaveCount(3);
    }

    // ── DeleteDirectoryAsync recursive — soft delete verification ────────────

    [Fact]
    public async Task DeleteDirectoryAsync_Recursive_FilesAreSoftDeleted()
    {
        var ownerId = Guid.NewGuid();
        var dir = await SeedDir(ownerId, "docs", "/docs");

        var file = new StorageFile
        {
            OwnerId = ownerId, FileName = "report.txt", NormalizedPath = "/docs/report.txt",
            Size = 50, StorageKey = "k1", BucketName = "dataguard-storage",
            ParentDirectoryId = dir.DirectoryId
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        await _repo.DeleteDirectoryAsync(dir.DirectoryId, ownerId, recursive: true);

        var deletedFile = await _db.Files.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.FileId == file.FileId);
        deletedFile.Should().NotBeNull();
        deletedFile!.DeletedAtUtc.Should().NotBeNull();

        var deletedDir = await _db.Directories.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.DirectoryId == dir.DirectoryId);
        deletedDir.Should().NotBeNull();
        deletedDir!.DeletedAtUtc.Should().NotBeNull();
    }
}