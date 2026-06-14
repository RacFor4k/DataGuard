using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageFileRepositoryTests : IDisposable
{
    private readonly StorageDbContext _db;
    private readonly StorageFileRepository _repo;

    public StorageFileRepositoryTests()
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

    [Fact]
    public async Task CreateFileAsync_ValidFile_PersistsInDatabase()
    {
        var file = new StorageFile
        {
            OwnerId = Guid.NewGuid(),
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "abc123",
            BucketName = "dataguard-storage"
        };

        var result = await _repo.CreateFileAsync(file);

        Assert.NotEqual(Guid.Empty, result.FileId);
        var stored = await _db.Files.FindAsync(result.FileId);
        Assert.NotNull(stored);
        Assert.Equal("test.txt", stored.FileName);
    }

    [Fact]
    public async Task GetFileAsync_ExistingFile_ReturnsFile()
    {
        var ownerId = Guid.NewGuid();
        var file = new StorageFile
        {
            OwnerId = ownerId,
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "abc123",
            BucketName = "dataguard-storage"
        };
        await _repo.CreateFileAsync(file);

        var result = await _repo.GetFileAsync(file.FileId, ownerId);

        Assert.NotNull(result);
        Assert.Equal("test.txt", result.FileName);
    }

    [Fact]
    public async Task GetFileAsync_WrongOwner_ReturnsNull()
    {
        var file = new StorageFile
        {
            OwnerId = Guid.NewGuid(),
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "abc123",
            BucketName = "dataguard-storage"
        };
        await _repo.CreateFileAsync(file);

        var result = await _repo.GetFileAsync(file.FileId, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteFileAsync_ExistingFile_SoftDeletes()
    {
        var ownerId = Guid.NewGuid();
        var file = new StorageFile
        {
            OwnerId = ownerId,
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "abc123",
            BucketName = "dataguard-storage"
        };
        await _repo.CreateFileAsync(file);

        var deleted = await _repo.DeleteFileAsync(file.FileId, ownerId);

        Assert.True(deleted);
        var stored = await _db.Files.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.FileId == file.FileId);
        Assert.NotNull(stored);
        Assert.NotNull(stored.DeletedAtUtc);
    }

    [Fact]
    public async Task SoftDelete_QueryFilter_ExcludesDeletedFiles()
    {
        var ownerId = Guid.NewGuid();
        var file1 = new StorageFile
        {
            OwnerId = ownerId,
            FileName = "active.txt",
            NormalizedPath = "/active.txt",
            Size = 100,
            StorageKey = "key1",
            BucketName = "dataguard-storage"
        };
        var file2 = new StorageFile
        {
            OwnerId = ownerId,
            FileName = "deleted.txt",
            NormalizedPath = "/deleted.txt",
            Size = 200,
            StorageKey = "key2",
            BucketName = "dataguard-storage"
        };
        await _repo.CreateFileAsync(file1);
        await _repo.CreateFileAsync(file2);
        await _repo.DeleteFileAsync(file2.FileId, ownerId);

        var files = await _repo.ListFilesAsync(ownerId, null, false);

        Assert.Single(files);
        Assert.Equal("active.txt", files[0].FileName);
    }
}
