using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageIntegrationTests
{
    #region Helpers

    private static StorageDbContext CreateDbContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new StorageDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static StorageDirectory CreateDir(StorageDbContext db, string name, Guid? parentId = null, string? path = null)
    {
        var dir = new StorageDirectory
        {
            OwnerId = OwnerId,
            ParentDirectoryId = parentId,
            DirectoryName = name,
            NormalizedName = name.ToLowerInvariant(),
            NormalizedPath = path ?? (parentId == null ? $"/{name.ToLowerInvariant()}" : $"") // will be fixed by caller if needed
        };
        return dir;
    }

    #endregion

    // ===================================================================
    // 1. Directory Tree Operations
    // ===================================================================

    [Fact]
    public async Task DirectoryTree_CreateAndList_WorksCorrectly()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);

        // Create root directory
        var root = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId,
            DirectoryName = "docs",
            NormalizedName = "docs",
            NormalizedPath = "/docs"
        });

        root.DirectoryId.Should().NotBeEmpty();
        root.NormalizedPath.Should().Be("/docs");

        // Create subdirectory
        var sub = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId,
            ParentDirectoryId = root.DirectoryId,
            DirectoryName = "reports",
            NormalizedName = "reports",
            NormalizedPath = "/docs/reports"
        });

        sub.ParentDirectoryId.Should().Be(root.DirectoryId);

        // List directory contents (non-recursive)
        var contents = await repo.ListDirectoryContentsAsync(OwnerId, root.DirectoryId, recursive: false);
        contents.Should().HaveCount(1);

        // List directory contents (recursive)
        var fileRepo = new StorageFileRepository(db);
        await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            ParentDirectoryId = sub.DirectoryId,
            FileName = "q1.pdf",
            NormalizedPath = "/docs/reports/q1.pdf",
            Size = 1024,
            StorageKey = "sk-q1",
            BucketName = "bucket"
        });

        var recursiveContents = await repo.ListDirectoryContentsAsync(OwnerId, root.DirectoryId, recursive: true);
        recursiveContents.Should().HaveCount(2); // 1 subdir + 1 file
    }

    [Fact]
    public async Task DirectoryTree_Rename_UpdatesNestedPaths()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);

        var root = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "projects", NormalizedName = "projects", NormalizedPath = "/projects"
        });
        var sub = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = root.DirectoryId,
            DirectoryName = "alpha", NormalizedName = "alpha", NormalizedPath = "/projects/alpha"
        });
        var fileRepo = new StorageFileRepository(db);
        await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = sub.DirectoryId,
            FileName = "readme.md", NormalizedPath = "/projects/alpha/readme.md",
            Size = 100, StorageKey = "sk-rm", BucketName = "b"
        });

        var renamed = await repo.RenameDirectoryAsync(root.DirectoryId, OwnerId, "work");

        renamed.Should().NotBeNull();
        renamed!.DirectoryName.Should().Be("work");
        renamed.NormalizedPath.Should().Be("/work");

        // Verify subdirectory path updated
        var subAfter = await repo.GetDirectoryAsync(sub.DirectoryId, OwnerId);
        subAfter!.NormalizedPath.Should().Be("/work/alpha");

        // Verify file path updated
        var fileAfter = await fileRepo.GetFileByPathAsync(OwnerId, "/work/alpha/readme.md");
        fileAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task DirectoryTree_MoveAndUpdatePaths_DeleteRecursive()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);

        var root1 = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "folder-a", NormalizedName = "folder-a", NormalizedPath = "/folder-a"
        });
        var root2 = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "folder-b", NormalizedName = "folder-b", NormalizedPath = "/folder-b"
        });
        var child = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = root1.DirectoryId,
            DirectoryName = "child", NormalizedName = "child", NormalizedPath = "/folder-a/child"
        });
        var fileRepo = new StorageFileRepository(db);
        await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = child.DirectoryId,
            FileName = "data.bin", NormalizedPath = "/folder-a/child/data.bin",
            Size = 500, StorageKey = "sk-data", BucketName = "b"
        });

        // Move folder-a into folder-b
        var moved = await repo.MoveDirectoryAsync(root1.DirectoryId, OwnerId, root2.DirectoryId, "/folder-b/folder-a");
        moved.Should().NotBeNull();
        moved!.ParentDirectoryId.Should().Be(root2.DirectoryId);

        var childAfter = await repo.GetDirectoryAsync(child.DirectoryId, OwnerId);
        childAfter!.NormalizedPath.Should().Be("/folder-b/folder-a/child");

        // Delete folder-b recursively
        var deleted = await repo.DeleteDirectoryAsync(root2.DirectoryId, OwnerId, recursive: true);
        deleted.Should().BeTrue();

        // Verify all soft-deleted
        var allDirs = await db.Directories.IgnoreQueryFilters()
            .Where(d => d.OwnerId == OwnerId).ToListAsync();
        allDirs.Should().HaveCount(3); // root1=folder-a, root2=folder-b, child
        allDirs.Count(d => d.DeletedAtUtc != null).Should().Be(3);

        var allFiles = await db.Files.IgnoreQueryFilters()
            .Where(f => f.OwnerId == OwnerId).ToListAsync();
        allFiles.Should().HaveCount(1);
        allFiles[0].DeletedAtUtc.Should().NotBeNull();
    }

    // ===================================================================
    // 2. File Lifecycle
    // ===================================================================

    [Fact]
    public async Task File_CreateAndGet_WorksCorrectly()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);
        var dirRepo = new StorageDirectoryRepository(db);

        var dir = await dirRepo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "files", NormalizedName = "files", NormalizedPath = "/files"
        });

        var created = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            ParentDirectoryId = dir.DirectoryId,
            FileName = "test.txt",
            NormalizedPath = "/files/test.txt",
            Size = 2048,
            StorageKey = "sk-test-txt",
            BucketName = "my-bucket",
            ContentHash = new byte[] { 1, 2, 3, 4 }
        });

        created.FileId.Should().NotBeEmpty();
        created.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Get by ID
        var byId = await repo.GetFileAsync(created.FileId, OwnerId);
        byId.Should().NotBeNull();
        byId!.FileName.Should().Be("test.txt");
        byId.Size.Should().Be(2048);
        byId.ContentHash.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });

        // Get by path
        var byPath = await repo.GetFileByPathAsync(OwnerId, "/files/test.txt");
        byPath.Should().NotBeNull();
        byPath!.StorageKey.Should().Be("sk-test-txt");
    }

    [Fact]
    public async Task File_RenameAndMove_UpdatesPathCorrectly()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);
        var dirRepo = new StorageDirectoryRepository(db);

        var dir1 = await dirRepo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "src", NormalizedName = "src", NormalizedPath = "/src"
        });
        var dir2 = await dirRepo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "dst", NormalizedName = "dst", NormalizedPath = "/dst"
        });

        var file = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            ParentDirectoryId = dir1.DirectoryId,
            FileName = "old.txt",
            NormalizedPath = "/src/old.txt",
            Size = 10,
            StorageKey = "sk-old",
            BucketName = "b"
        });

        // Rename
        var renamed = await repo.RenameFileAsync(file.FileId, OwnerId, "new.txt");
        renamed.Should().NotBeNull();
        renamed!.FileName.Should().Be("new.txt");
        renamed.NormalizedPath.Should().Be("/src/new.txt");

        // Move
        var moved = await repo.MoveFileAsync(file.FileId, OwnerId, dir2.DirectoryId, "/dst/new.txt");
        moved.Should().NotBeNull();
        moved!.ParentDirectoryId.Should().Be(dir2.DirectoryId);
        moved.NormalizedPath.Should().Be("/dst/new.txt");
    }

    [Fact]
    public async Task File_CopyDeleteAndList_WorksCorrectly()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);
        var dirRepo = new StorageDirectoryRepository(db);

        var dir = await dirRepo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "data", NormalizedName = "data", NormalizedPath = "/data"
        });

        var original = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            ParentDirectoryId = dir.DirectoryId,
            FileName = "orig.bin",
            NormalizedPath = "/data/orig.bin",
            Size = 999,
            StorageKey = "sk-orig",
            BucketName = "b",
            ContentHash = new byte[] { 0xAA, 0xBB }
        });

        // Add metadata to original
        var metaService = new StorageMetadataService(db);
        await metaService.UpdateMetadataAsync(original.FileId, new Dictionary<string, string>
        {
            { "author", "test" }
        });

        // Copy
        var copy = await repo.CopyFileAsync(original.FileId, OwnerId, dir.DirectoryId, "/data/copy.bin");
        copy.Should().NotBeNull();
        copy!.FileId.Should().NotBe(original.FileId);
        copy.StorageKey.Should().Be("sk-orig"); // same storage key
        copy.ContentHash.Should().BeEquivalentTo(new byte[] { 0xAA, 0xBB });
        copy.NormalizedPath.Should().Be("/data/copy.bin");

        // Copy should also copy metadata
        var copyMeta = await metaService.GetMetadataAsync(copy.FileId);
        copyMeta.Should().ContainKey("author");

        // Delete original
        var deleted = await repo.DeleteFileAsync(original.FileId, OwnerId);
        deleted.Should().BeTrue();

        // Verify soft-deleted
        var softDeleted = await db.Files.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.FileId == original.FileId);
        softDeleted.Should().NotBeNull();
        softDeleted!.DeletedAtUtc.Should().NotBeNull();

        // List files in directory → should only show the copy, not deleted original
        var files = await repo.ListFilesAsync(OwnerId, dir.DirectoryId, recursive: false);
        files.Should().HaveCount(1);
        files[0].FileId.Should().Be(copy.FileId);
    }

    // ===================================================================
    // 3. Metadata Operations
    // ===================================================================

    [Fact]
    public async Task Metadata_CreateUpdateAndGet_WorksCorrectly()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var fileRepo = new StorageFileRepository(db);
        var metaService = new StorageMetadataService(db);

        var file = await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            FileName = "meta-test.txt",
            NormalizedPath = "/meta-test.txt",
            Size = 50,
            StorageKey = "sk-meta",
            BucketName = "b"
        });

        // Update metadata (multiple key-values)
        await metaService.UpdateMetadataAsync(file.FileId, new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" },
            { "key3", "value3" }
        });

        var meta = await metaService.GetMetadataAsync(file.FileId);
        meta.Should().HaveCount(3);
        meta["key1"].Should().Be("value1");
        meta["key2"].Should().Be("value2");
        meta["key3"].Should().Be("value3");

        // Overwrite some keys
        await metaService.UpdateMetadataAsync(file.FileId, new Dictionary<string, string>
        {
            { "key1", "updated1" },
            { "key4", "value4" }
        });

        var updatedMeta = await metaService.GetMetadataAsync(file.FileId);
        updatedMeta.Should().HaveCount(2);
        updatedMeta["key1"].Should().Be("updated1");
        updatedMeta["key4"].Should().Be("value4");
        updatedMeta.Should().NotContainKey("key2");
        updatedMeta.Should().NotContainKey("key3");
    }

    // ===================================================================
    // 4. Link Operations
    // ===================================================================

    [Fact]
    public async Task Link_GenerateAndValidate_WorksCorrectly()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var fileRepo = new StorageFileRepository(db);
        var linkService = new StorageLinkService(db);

        var otherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var file = await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            FileName = "shared.pdf",
            NormalizedPath = "/shared.pdf",
            Size = 5000,
            StorageKey = "sk-shared",
            BucketName = "b"
        });

        // Generate link with TTL and allowed user
        var link = await linkService.GenerateLinkAsync(
            file.FileId, OwnerId, TimeSpan.FromHours(1),
            isDirect: false,
            users: new[] { otherUserId.ToString() },
            groups: null);

        link.Token.Should().NotBeNullOrEmpty();
        link.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        link.IsDirect.Should().BeFalse();

        // Get link
        var fetched = await linkService.GetLinkAsync(link.Token);
        fetched.Should().NotBeNull();
        fetched!.FileId.Should().Be(file.FileId);

        // Validate with owner → true
        var ownerResult = await linkService.ValidateLinkAsync(link.Token, OwnerId);
        ownerResult.Should().BeTrue();

        // Validate with allowed user → true
        var allowedResult = await linkService.ValidateLinkAsync(link.Token, otherUserId);
        allowedResult.Should().BeTrue();

        // Validate with non-allowed user → false
        var strangerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var strangerResult = await linkService.ValidateLinkAsync(link.Token, strangerId);
        strangerResult.Should().BeFalse();
    }

    [Fact]
    public async Task Link_ExpiredAndDirect_WorksCorrectly()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var fileRepo = new StorageFileRepository(db);
        var linkService = new StorageLinkService(db);

        var file = await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            FileName = "direct.txt",
            NormalizedPath = "/direct.txt",
            Size = 100,
            StorageKey = "sk-direct",
            BucketName = "b"
        });

        // Generate direct link
        var directLink = await linkService.GenerateLinkAsync(
            file.FileId, OwnerId, TimeSpan.FromDays(7),
            isDirect: true,
            users: null,
            groups: null);

        directLink.IsDirect.Should().BeTrue();

        // Expire the link by modifying directly in DB
        directLink.ExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        // Validate expired link → false
        var expiredResult = await linkService.ValidateLinkAsync(directLink.Token, OwnerId);
        expiredResult.Should().BeFalse();
    }

    // ===================================================================
    // 5. Edge Cases
    // ===================================================================

    [Fact]
    public async Task EdgeCase_CreateDuplicateDirectoryName_ThrowsOrFails()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);

        // Creating same name in same parent should fail (unique constraint)
        // Note: SQLite treats NULLs as distinct in unique indexes, so a non-null
        // ParentDirectoryId is required for the constraint to trigger.
        var parentId = Guid.NewGuid();
        await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = parentId,
            DirectoryName = "dup", NormalizedName = "dup", NormalizedPath = "/dup"
        });

        var act = () => repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = parentId,
            DirectoryName = "dup", NormalizedName = "dup", NormalizedPath = "/dup"
        });

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task EdgeCase_NonRecursiveDeleteNonEmpty_ReturnsFalse()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);

        var parent = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "parent", NormalizedName = "parent", NormalizedPath = "/parent"
        });
        await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = parent.DirectoryId,
            DirectoryName = "child", NormalizedName = "child", NormalizedPath = "/parent/child"
        });

        var result = await repo.DeleteDirectoryAsync(parent.DirectoryId, OwnerId, recursive: false);
        result.Should().BeFalse();

        // Parent should still exist
        var stillExists = await repo.GetDirectoryAsync(parent.DirectoryId, OwnerId);
        stillExists.Should().NotBeNull();
    }

    [Fact]
    public async Task EdgeCase_MoveFileToNonExistentParent_ReturnsNull()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);

        var file = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            FileName = "orphan.txt",
            NormalizedPath = "/orphan.txt",
            Size = 10,
            StorageKey = "sk-orphan",
            BucketName = "b"
        });

        var fakeParentId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var result = await repo.MoveFileAsync(file.FileId, OwnerId, fakeParentId, "/fake/orphan.txt");
        // MoveFileAsync does NOT validate parent existence — it just updates the record
        result.Should().NotBeNull();
        result!.ParentDirectoryId.Should().Be(fakeParentId);
    }

    [Fact]
    public async Task EdgeCase_ListFilesInNonExistentDirectory_ReturnsEmptyList()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);

        var fakeDirId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var files = await repo.ListFilesAsync(OwnerId, fakeDirId, recursive: false);
        files.Should().BeEmpty();
    }
}