using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageMetadataServiceTests : IDisposable
{
    private readonly StorageDbContext _db;
    private readonly StorageMetadataService _service;

    public StorageMetadataServiceTests()
    {
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new StorageDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _service = new StorageMetadataService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task UpdateMetadataAsync_ValidMetadata_Persists()
    {
        var fileId = Guid.NewGuid();
        var file = new StorageFile
        {
            FileId = fileId,
            OwnerId = Guid.NewGuid(),
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "key1",
            BucketName = "dataguard-storage"
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        var metadata = new Dictionary<string, string>
        {
            ["author"] = "test-user",
            ["version"] = "1.0"
        };

        await _service.UpdateMetadataAsync(fileId, metadata);

        var stored = await _service.GetMetadataAsync(fileId);
        Assert.Equal(2, stored.Count);
        Assert.Equal("test-user", stored["author"]);
    }

    [Fact]
    public async Task UpdateMetadataAsync_ReservedKey_ThrowsException()
    {
        var fileId = Guid.NewGuid();
        var file = new StorageFile
        {
            FileId = fileId,
            OwnerId = Guid.NewGuid(),
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "key1",
            BucketName = "dataguard-storage"
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        var metadata = new Dictionary<string, string>
        {
            ["storageKey"] = "value"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateMetadataAsync(fileId, metadata));
    }

    [Fact]
    public async Task UpdateMetadataAsync_PrefixDoubleUnderscore_ThrowsException()
    {
        var fileId = Guid.NewGuid();
        var file = new StorageFile
        {
            FileId = fileId,
            OwnerId = Guid.NewGuid(),
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "key1",
            BucketName = "dataguard-storage"
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        var metadata = new Dictionary<string, string>
        {
            ["__internal"] = "value"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateMetadataAsync(fileId, metadata));
    }

    [Fact]
    public async Task UpdateMetadataAsync_TooManyKeys_ThrowsException()
    {
        var fileId = Guid.NewGuid();
        var file = new StorageFile
        {
            FileId = fileId,
            OwnerId = Guid.NewGuid(),
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "key1",
            BucketName = "dataguard-storage"
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        var metadata = new Dictionary<string, string>();
        for (int i = 0; i < 65; i++)
            metadata[$"key{i}"] = "value";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateMetadataAsync(fileId, metadata));
    }

    [Fact]
    public async Task UpdateMetadataAsync_OverwritesExisting()
    {
        var fileId = Guid.NewGuid();
        var file = new StorageFile
        {
            FileId = fileId,
            OwnerId = Guid.NewGuid(),
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "key1",
            BucketName = "dataguard-storage"
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        await _service.UpdateMetadataAsync(fileId, new Dictionary<string, string> { ["key1"] = "v1" });
        await _service.UpdateMetadataAsync(fileId, new Dictionary<string, string> { ["key2"] = "v2" });

        var stored = await _service.GetMetadataAsync(fileId);
        Assert.Single(stored);
        Assert.Equal("v2", stored["key2"]);
    }
}
