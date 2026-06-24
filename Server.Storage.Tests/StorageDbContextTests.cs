using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;

namespace Server.Storage.Tests;

public class StorageDbContextTests : IDisposable
{
    private readonly StorageDbContext _db;

    public StorageDbContextTests()
    {
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new StorageDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task EnsureCreated_CreatesTables()
    {
        Assert.True(await _db.Database.CanConnectAsync());
    }

    [Fact]
    public async Task StorageFile_WithMetadata_PersistsAndRetrieves()
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
            BucketName = "dataguard-storage",
            Metadata = new List<FileMetadataEntry>
            {
                new() { Id = Guid.NewGuid(), Key = "author", Value = "test-user" }
            }
        };

        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        var stored = await _db.Files.Include(f => f.Metadata).FirstOrDefaultAsync(f => f.FileId == fileId);
        Assert.NotNull(stored);
        Assert.Single(stored.Metadata);
        Assert.Equal("author", stored.Metadata.First().Key);
    }

    [Fact]
    public async Task StorageSharedLink_UniqueToken_Persists()
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

        var link = new StorageSharedLink
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            OwnerId = Guid.NewGuid(),
            Token = "unique-token-123",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            IsDirect = false
        };

        _db.SharedLinks.Add(link);
        await _db.SaveChangesAsync();

        var stored = await _db.SharedLinks.FirstOrDefaultAsync(l => l.Token == "unique-token-123");
        Assert.NotNull(stored);
        Assert.Equal("unique-token-123", stored.Token);
    }
}
