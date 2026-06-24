using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

/// <summary>
/// Repository-level integration tests using a real SQLite in-memory database.
/// Exercises StorageFileRepository and StorageDirectoryRepository end-to-end.
/// </summary>
public class StorageRepositoryIntegrationTests
{
    #region Helpers

    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherOwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

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

    #endregion

    // ===================================================================
    // StorageFileRepository Tests
    // ===================================================================

    // 1. Create + Get
    [Fact]
    public async Task FileRepo_CreateAndGet_ReturnsFileWithCorrectFields()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);

        var created = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            FileName = "hello.txt",
            NormalizedPath = "/hello.txt",
            Size = 42,
            StorageKey = "sk-hello",
            BucketName = "test-bucket",
            ContentHash = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }
        });

        created.FileId.Should().NotBeEmpty();
        created.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var fetched = await repo.GetFileAsync(created.FileId, OwnerId);
        fetched.Should().NotBeNull();
        fetched!.FileName.Should().Be("hello.txt");
        fetched.NormalizedPath.Should().Be("/hello.txt");
        fetched.Size.Should().Be(42);
        fetched.StorageKey.Should().Be("sk-hello");
        fetched.BucketName.Should().Be("test-bucket");
        fetched.ContentHash.Should().BeEquivalentTo(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        fetched.ParentDirectoryId.Should().BeNull();
        fetched.DeletedAtUtc.Should().BeNull();
    }

    // 2. Wrong owner → null
    [Fact]
    public async Task FileRepo_GetWithWrongOwner_ReturnsNull()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);

        var created = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            FileName = "secret.txt",
            NormalizedPath = "/secret.txt",
            Size = 100,
            StorageKey = "sk-secret",
            BucketName = "b"
        });

        var result = await repo.GetFileAsync(created.FileId, OtherOwnerId);
        result.Should().BeNull();
    }

    // 3. Update fields
    [Fact]
    public async Task FileRepo_Update_ModifiesFieldsAndReturnsUpdatedEntity()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);
        var dirRepo = new StorageDirectoryRepository(db);

        var dir = await dirRepo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId,
            DirectoryName = "docs",
            NormalizedName = "docs",
            NormalizedPath = "/docs"
        });

        var created = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            ParentDirectoryId = dir.DirectoryId,
            FileName = "draft.txt",
            NormalizedPath = "/docs/draft.txt",
            Size = 50,
            StorageKey = "sk-draft",
            BucketName = "b"
        });

        var update = new StorageFile
        {
            FileId = created.FileId,
            OwnerId = OwnerId,
            FileName = "final.txt",
            NormalizedPath = "/docs/final.txt",
            ParentDirectoryId = dir.DirectoryId,
            Size = 200,
            StorageKey = "sk-final",
            BucketName = "b-prod",
            ContentHash = new byte[] { 0x01, 0x02 }
        };

        var updated = await repo.UpdateFileAsync(update);
        updated.Should().NotBeNull();
        updated!.FileName.Should().Be("final.txt");
        updated.NormalizedPath.Should().Be("/docs/final.txt");
        updated.Size.Should().Be(200);
        updated.StorageKey.Should().Be("sk-final");
        updated.BucketName.Should().Be("b-prod");
        updated.ContentHash.Should().BeEquivalentTo(new byte[] { 0x01, 0x02 });
        updated.UpdatedAtUtc.Should().NotBeNull();

        // Verify persisted
        var fetched = await repo.GetFileAsync(created.FileId, OwnerId);
        fetched!.FileName.Should().Be("final.txt");
        fetched.Size.Should().Be(200);
    }

    // 4. Soft delete
    [Fact]
    public async Task FileRepo_SoftDelete_SetsDeletedAtUtc()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);

        var created = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId,
            FileName = "temp.txt",
            NormalizedPath = "/temp.txt",
            Size = 10,
            StorageKey = "sk-temp",
            BucketName = "b"
        });

        var deleted = await repo.DeleteFileAsync(created.FileId, OwnerId);
        deleted.Should().BeTrue();

        // Regular query should not find it (global query filter)
        var fetched = await repo.GetFileAsync(created.FileId, OwnerId);
        fetched.Should().BeNull();

        // Bypassing query filter should show it's soft-deleted
        var raw = await db.Files.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.FileId == created.FileId);
        raw.Should().NotBeNull();
        raw!.DeletedAtUtc.Should().NotBeNull();
        raw.DeletedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // 5. List excludes deleted
    [Fact]
    public async Task FileRepo_ListFiles_ExcludesSoftDeleted()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageFileRepository(db);
        var dirRepo = new StorageDirectoryRepository(db);

        var dir = await dirRepo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId,
            DirectoryName = "archive",
            NormalizedName = "archive",
            NormalizedPath = "/archive"
        });

        var file1 = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = dir.DirectoryId,
            FileName = "keep.txt", NormalizedPath = "/archive/keep.txt",
            Size = 1, StorageKey = "sk-k", BucketName = "b"
        });
        var file2 = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = dir.DirectoryId,
            FileName = "remove.txt", NormalizedPath = "/archive/remove.txt",
            Size = 2, StorageKey = "sk-r", BucketName = "b"
        });
        var file3 = await repo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = dir.DirectoryId,
            FileName = "also-keep.txt", NormalizedPath = "/archive/also-keep.txt",
            Size = 3, StorageKey = "sk-ak", BucketName = "b"
        });

        // Delete file2
        await repo.DeleteFileAsync(file2.FileId, OwnerId);

        // List files in directory
        var listed = await repo.ListFilesAsync(OwnerId, dir.DirectoryId, recursive: false);
        listed.Should().HaveCount(2);
        listed.Select(f => f.FileId).Should().Contain(file1.FileId);
        listed.Select(f => f.FileId).Should().Contain(file3.FileId);
        listed.Select(f => f.FileId).Should().NotContain(file2.FileId);
    }

    // ===================================================================
    // StorageDirectoryRepository Tests
    // ===================================================================

    // 1. Create + Get
    [Fact]
    public async Task DirRepo_CreateAndGet_ReturnsCorrectDirectory()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);

        var created = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId,
            DirectoryName = "projects",
            NormalizedName = "projects",
            NormalizedPath = "/projects"
        });

        created.DirectoryId.Should().NotBeEmpty();
        created.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var fetched = await repo.GetDirectoryAsync(created.DirectoryId, OwnerId);
        fetched.Should().NotBeNull();
        fetched!.DirectoryName.Should().Be("projects");
        fetched.NormalizedName.Should().Be("projects");
        fetched.NormalizedPath.Should().Be("/projects");
        fetched.ParentDirectoryId.Should().BeNull();
    }

    // 2. GetByPath
    [Fact]
    public async Task DirRepo_GetByPath_ReturnsCorrectDirectory()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);

        await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId,
            DirectoryName = "music",
            NormalizedName = "music",
            NormalizedPath = "/music"
        });

        var fetched = await repo.GetDirectoryByPathAsync(OwnerId, "/music");
        fetched.Should().NotBeNull();
        fetched!.DirectoryName.Should().Be("music");

        // Non-existent path
        var missing = await repo.GetDirectoryByPathAsync(OwnerId, "/nonexistent");
        missing.Should().BeNull();
    }

    // 3. Rename updates children paths
    [Fact]
    public async Task DirRepo_Rename_UpdatesChildrenPaths()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);
        var fileRepo = new StorageFileRepository(db);

        var root = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "src", NormalizedName = "src", NormalizedPath = "/src"
        });
        var child = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = root.DirectoryId,
            DirectoryName = "lib", NormalizedName = "lib", NormalizedPath = "/src/lib"
        });
        var grandchild = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = child.DirectoryId,
            DirectoryName = "core", NormalizedName = "core", NormalizedPath = "/src/lib/core"
        });

        await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = child.DirectoryId,
            FileName = "utils.cs", NormalizedPath = "/src/lib/utils.cs",
            Size = 500, StorageKey = "sk-utils", BucketName = "b"
        });

        var renamed = await repo.RenameDirectoryAsync(root.DirectoryId, OwnerId, "source");
        renamed.Should().NotBeNull();
        renamed!.DirectoryName.Should().Be("source");
        renamed.NormalizedPath.Should().Be("/source");

        // Verify child path updated
        var childAfter = await repo.GetDirectoryAsync(child.DirectoryId, OwnerId);
        childAfter!.NormalizedPath.Should().Be("/source/lib");

        // Verify grandchild path updated
        var grandchildAfter = await repo.GetDirectoryAsync(grandchild.DirectoryId, OwnerId);
        grandchildAfter!.NormalizedPath.Should().Be("/source/lib/core");

        // Verify file path updated
        var fileAfter = await fileRepo.GetFileByPathAsync(OwnerId, "/source/lib/utils.cs");
        fileAfter.Should().NotBeNull();
        fileAfter!.FileName.Should().Be("utils.cs");

        // Old path should no longer exist
        var oldPath = await fileRepo.GetFileByPathAsync(OwnerId, "/src/lib/utils.cs");
        oldPath.Should().BeNull();
    }

    // 4. Move updates paths
    [Fact]
    public async Task DirRepo_Move_UpdatesAllNestedPaths()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);
        var fileRepo = new StorageFileRepository(db);

        var folderA = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "folder-a", NormalizedName = "folder-a", NormalizedPath = "/folder-a"
        });
        var folderB = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "folder-b", NormalizedName = "folder-b", NormalizedPath = "/folder-b"
        });
        var subA = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = folderA.DirectoryId,
            DirectoryName = "sub-a", NormalizedName = "sub-a", NormalizedPath = "/folder-a/sub-a"
        });

        await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = folderA.DirectoryId,
            FileName = "root-file.txt", NormalizedPath = "/folder-a/root-file.txt",
            Size = 10, StorageKey = "sk-rf", BucketName = "b"
        });
        await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = subA.DirectoryId,
            FileName = "nested-file.txt", NormalizedPath = "/folder-a/sub-a/nested-file.txt",
            Size = 20, StorageKey = "sk-nf", BucketName = "b"
        });

        // Move folder-a into folder-b
        var moved = await repo.MoveDirectoryAsync(folderA.DirectoryId, OwnerId, folderB.DirectoryId, "/folder-b/folder-a");
        moved.Should().NotBeNull();
        moved!.ParentDirectoryId.Should().Be(folderB.DirectoryId);
        moved.NormalizedPath.Should().Be("/folder-b/folder-a");

        // Verify subdirectory path
        var subAfter = await repo.GetDirectoryAsync(subA.DirectoryId, OwnerId);
        subAfter!.NormalizedPath.Should().Be("/folder-b/folder-a/sub-a");

        // Verify root file path
        var rootFile = await fileRepo.GetFileByPathAsync(OwnerId, "/folder-b/folder-a/root-file.txt");
        rootFile.Should().NotBeNull();

        // Verify nested file path
        var nestedFile = await fileRepo.GetFileByPathAsync(OwnerId, "/folder-b/folder-a/sub-a/nested-file.txt");
        nestedFile.Should().NotBeNull();

        // Old paths should not exist
        (await fileRepo.GetFileByPathAsync(OwnerId, "/folder-a/root-file.txt")).Should().BeNull();
        (await fileRepo.GetFileByPathAsync(OwnerId, "/folder-a/sub-a/nested-file.txt")).Should().BeNull();
    }

    // 5. GetSubdirectories
    [Fact]
    public async Task DirRepo_GetSubdirectories_ReturnsOnlyDirectChildren()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);

        var root = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "root", NormalizedName = "root", NormalizedPath = "/root"
        });
        var child1 = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = root.DirectoryId,
            DirectoryName = "child1", NormalizedName = "child1", NormalizedPath = "/root/child1"
        });
        var child2 = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = root.DirectoryId,
            DirectoryName = "child2", NormalizedName = "child2", NormalizedPath = "/root/child2"
        });
        var grandchild = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = child1.DirectoryId,
            DirectoryName = "grandchild", NormalizedName = "grandchild", NormalizedPath = "/root/child1/grandchild"
        });

        // Get subdirectories of root (null parent = root level)
        var rootLevel = await repo.GetSubdirectoriesAsync(OwnerId, null);
        rootLevel.Should().HaveCount(1);
        rootLevel[0].DirectoryId.Should().Be(root.DirectoryId);

        // Get subdirectories of root directory
        var children = await repo.GetSubdirectoriesAsync(OwnerId, root.DirectoryId);
        children.Should().HaveCount(2);
        children.Select(d => d.DirectoryId).Should().Contain(child1.DirectoryId);
        children.Select(d => d.DirectoryId).Should().Contain(child2.DirectoryId);
        children.Select(d => d.DirectoryId).Should().NotContain(grandchild.DirectoryId);

        // Get subdirectories of child1
        var grandchildren = await repo.GetSubdirectoriesAsync(OwnerId, child1.DirectoryId);
        grandchildren.Should().HaveCount(1);
        grandchildren[0].DirectoryId.Should().Be(grandchild.DirectoryId);

        // Other owner should see nothing
        var otherOwnerChildren = await repo.GetSubdirectoriesAsync(OtherOwnerId, root.DirectoryId);
        otherOwnerChildren.Should().BeEmpty();
    }

    // 6. Delete recursive marks all deleted
    [Fact]
    public async Task DirRepo_DeleteRecursive_MarksAllChildrenAsDeleted()
    {
        using var db = CreateDbContext(out var conn);
        await using var _ = conn.ConfigureAwait(false);
        var repo = new StorageDirectoryRepository(db);
        var fileRepo = new StorageFileRepository(db);

        var root = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "to-delete", NormalizedName = "to-delete", NormalizedPath = "/to-delete"
        });
        var sub1 = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = root.DirectoryId,
            DirectoryName = "sub1", NormalizedName = "sub1", NormalizedPath = "/to-delete/sub1"
        });
        var sub2 = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = root.DirectoryId,
            DirectoryName = "sub2", NormalizedName = "sub2", NormalizedPath = "/to-delete/sub2"
        });
        var deepChild = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, ParentDirectoryId = sub1.DirectoryId,
            DirectoryName = "deep", NormalizedName = "deep", NormalizedPath = "/to-delete/sub1/deep"
        });

        var file1 = await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = root.DirectoryId,
            FileName = "f1.txt", NormalizedPath = "/to-delete/f1.txt",
            Size = 10, StorageKey = "sk-f1", BucketName = "b"
        });
        var file2 = await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = sub1.DirectoryId,
            FileName = "f2.txt", NormalizedPath = "/to-delete/sub1/f2.txt",
            Size = 20, StorageKey = "sk-f2", BucketName = "b"
        });
        var file3 = await fileRepo.CreateFileAsync(new StorageFile
        {
            OwnerId = OwnerId, ParentDirectoryId = deepChild.DirectoryId,
            FileName = "f3.txt", NormalizedPath = "/to-delete/sub1/deep/f3.txt",
            Size = 30, StorageKey = "sk-f3", BucketName = "b"
        });

        // Create a separate directory that should NOT be affected
        var safe = await repo.CreateDirectoryAsync(new StorageDirectory
        {
            OwnerId = OwnerId, DirectoryName = "safe", NormalizedName = "safe", NormalizedPath = "/safe"
        });

        // Delete recursively
        var result = await repo.DeleteDirectoryAsync(root.DirectoryId, OwnerId, recursive: true);
        result.Should().BeTrue();

        // Verify all directories in the tree are soft-deleted
        var allDirs = await db.Directories.IgnoreQueryFilters()
            .Where(d => d.OwnerId == OwnerId).ToListAsync();
        var deletedDirIds = allDirs
            .Where(d => d.DeletedAtUtc != null)
            .Select(d => d.DirectoryId)
            .ToHashSet();
        deletedDirIds.Should().Contain(root.DirectoryId);
        deletedDirIds.Should().Contain(sub1.DirectoryId);
        deletedDirIds.Should().Contain(sub2.DirectoryId);
        deletedDirIds.Should().Contain(deepChild.DirectoryId);
        deletedDirIds.Should().NotContain(safe.DirectoryId);

        // Verify all files are soft-deleted
        var allFiles = await db.Files.IgnoreQueryFilters()
            .Where(f => f.OwnerId == OwnerId).ToListAsync();
        var deletedFileIds = allFiles
            .Where(f => f.DeletedAtUtc != null)
            .Select(f => f.FileId)
            .ToHashSet();
        deletedFileIds.Should().Contain(file1.FileId);
        deletedFileIds.Should().Contain(file2.FileId);
        deletedFileIds.Should().Contain(file3.FileId);

        // Regular queries should not find any deleted items
        (await repo.GetDirectoryAsync(root.DirectoryId, OwnerId)).Should().BeNull();
        (await repo.GetDirectoryAsync(sub1.DirectoryId, OwnerId)).Should().BeNull();
        (await fileRepo.GetFileAsync(file1.FileId, OwnerId)).Should().BeNull();
        (await fileRepo.GetFileAsync(file2.FileId, OwnerId)).Should().BeNull();
        (await fileRepo.GetFileAsync(file3.FileId, OwnerId)).Should().BeNull();

        // Safe directory should still be accessible
        (await repo.GetDirectoryAsync(safe.DirectoryId, OwnerId)).Should().NotBeNull();
    }
}