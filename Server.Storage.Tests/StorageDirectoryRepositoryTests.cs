using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageDirectoryRepositoryTests : IDisposable
{
    private readonly StorageDbContext _db;
    private readonly StorageDirectoryRepository _repo;

    public StorageDirectoryRepositoryTests()
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

    [Fact]
    public async Task CreateDirectoryAsync_ValidDirectory_PersistsInDatabase()
    {
        var ownerId = Guid.NewGuid();
        var dir = new StorageDirectory
        {
            OwnerId = ownerId,
            DirectoryName = "docs",
            NormalizedPath = "/docs"
        };

        var result = await _repo.CreateDirectoryAsync(dir);

        Assert.NotEqual(Guid.Empty, result.DirectoryId);
        var stored = await _db.Directories.FindAsync(result.DirectoryId);
        Assert.NotNull(stored);
        Assert.Equal("docs", stored.DirectoryName);
    }

    [Fact]
    public async Task GetDirectoryAsync_WrongOwner_ReturnsNull()
    {
        var dir = new StorageDirectory
        {
            OwnerId = Guid.NewGuid(),
            DirectoryName = "docs",
            NormalizedPath = "/docs"
        };
        await _repo.CreateDirectoryAsync(dir);

        var result = await _repo.GetDirectoryAsync(dir.DirectoryId, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteDirectoryAsync_NonEmptyNotRecursive_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var dir = new StorageDirectory
        {
            OwnerId = ownerId,
            DirectoryName = "docs",
            NormalizedPath = "/docs"
        };
        await _repo.CreateDirectoryAsync(dir);

        var childDir = new StorageDirectory
        {
            OwnerId = ownerId,
            DirectoryName = "projects",
            NormalizedPath = "/docs/projects",
            ParentDirectoryId = dir.DirectoryId
        };
        await _repo.CreateDirectoryAsync(childDir);

        var result = await _repo.DeleteDirectoryAsync(dir.DirectoryId, ownerId, false);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteDirectoryAsync_Recursive_SoftDeletesAll()
    {
        var ownerId = Guid.NewGuid();
        var dir = new StorageDirectory
        {
            OwnerId = ownerId,
            DirectoryName = "docs",
            NormalizedPath = "/docs"
        };
        await _repo.CreateDirectoryAsync(dir);

        var childDir = new StorageDirectory
        {
            OwnerId = ownerId,
            DirectoryName = "projects",
            NormalizedPath = "/docs/projects",
            ParentDirectoryId = dir.DirectoryId
        };
        await _repo.CreateDirectoryAsync(childDir);

        var result = await _repo.DeleteDirectoryAsync(dir.DirectoryId, ownerId, true);

        Assert.True(result);
    }

    [Fact]
    public async Task RenameDirectoryAsync_ValidName_UpdatesNestedPaths()
    {
        var ownerId = Guid.NewGuid();
        var dir = new StorageDirectory
        {
            OwnerId = ownerId,
            DirectoryName = "docs",
            NormalizedPath = "/docs"
        };
        await _repo.CreateDirectoryAsync(dir);

        var childDir = new StorageDirectory
        {
            OwnerId = ownerId,
            DirectoryName = "projects",
            NormalizedPath = "/docs/projects",
            ParentDirectoryId = dir.DirectoryId
        };
        await _repo.CreateDirectoryAsync(childDir);

        var file = new StorageFile
        {
            OwnerId = ownerId,
            FileName = "report.txt",
            NormalizedPath = "/docs/projects/report.txt",
            Size = 100,
            StorageKey = "key1",
            BucketName = "dataguard-storage",
            ParentDirectoryId = childDir.DirectoryId
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        await _repo.RenameDirectoryAsync(dir.DirectoryId, ownerId, "documents");

        var updatedChild = await _db.Directories.IgnoreQueryFilters().FirstAsync(d => d.DirectoryId == childDir.DirectoryId);
        Assert.Equal("/documents/projects", updatedChild.NormalizedPath);

        var updatedFile = await _db.Files.IgnoreQueryFilters().FirstAsync(f => f.FileId == file.FileId);
        Assert.Equal("/documents/projects/report.txt", updatedFile.NormalizedPath);
    }
}
