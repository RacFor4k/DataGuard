using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Interfaces;
using Server.Storage.Models;

namespace Server.Storage.Services;

public class StorageDirectoryRepository : IStorageDirectoryRepository
{
    private readonly StorageDbContext _db;

    public StorageDirectoryRepository(StorageDbContext db)
    {
        _db = db;
    }

    public async Task<StorageDirectory?> GetDirectoryAsync(Guid directoryId, Guid ownerId, CancellationToken ct = default)
    {
        return await _db.Directories.FirstOrDefaultAsync(d => d.DirectoryId == directoryId && d.OwnerId == ownerId, ct);
    }

    public async Task<StorageDirectory?> GetDirectoryByPathAsync(Guid ownerId, string normalizedPath, CancellationToken ct = default)
    {
        return await _db.Directories.FirstOrDefaultAsync(d => d.OwnerId == ownerId && d.NormalizedPath == normalizedPath, ct);
    }

    public async Task<StorageDirectory> CreateDirectoryAsync(StorageDirectory directory, CancellationToken ct = default)
    {
        directory.DirectoryId = Guid.NewGuid();
        directory.CreatedAtUtc = DateTime.UtcNow;
        _db.Directories.Add(directory);
        await _db.SaveChangesAsync(ct);
        return directory;
    }

    public async Task<bool> DeleteDirectoryAsync(Guid directoryId, Guid ownerId, bool recursive, CancellationToken ct = default)
    {
        var dir = await _db.Directories.FirstOrDefaultAsync(d => d.DirectoryId == directoryId && d.OwnerId == ownerId, ct);
        if (dir == null) return false;

        if (!recursive)
        {
            bool hasChildren = await _db.Directories.AnyAsync(d => d.ParentDirectoryId == directoryId, ct)
                || await _db.Files.AnyAsync(f => f.ParentDirectoryId == directoryId, ct);
            if (hasChildren) return false;
        }

        await DeleteRecursiveAsync(dir, ownerId, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task DeleteRecursiveAsync(StorageDirectory dir, Guid ownerId, CancellationToken ct)
    {
        var subdirs = await _db.Directories.Where(d => d.ParentDirectoryId == dir.DirectoryId && d.OwnerId == ownerId).ToListAsync(ct);
        foreach (var sub in subdirs)
            await DeleteRecursiveAsync(sub, ownerId, ct);

        var files = await _db.Files.Where(f => f.ParentDirectoryId == dir.DirectoryId && f.OwnerId == ownerId).ToListAsync(ct);
        foreach (var file in files)
            file.DeletedAtUtc = DateTime.UtcNow;

        dir.DeletedAtUtc = DateTime.UtcNow;
    }

    public async Task<StorageDirectory?> RenameDirectoryAsync(Guid directoryId, Guid ownerId, string newName, CancellationToken ct = default)
    {
        var dir = await _db.Directories.FirstOrDefaultAsync(d => d.DirectoryId == directoryId && d.OwnerId == ownerId, ct);
        if (dir == null) return null;

        string oldPath = dir.NormalizedPath;
        string parentPath = Path.GetDirectoryName(oldPath)?.Replace('\\', '/') ?? "/";
        if (!parentPath.StartsWith("/")) parentPath = "/" + parentPath;

        string newPath = parentPath == "/" ? $"/{newName}" : $"{parentPath}/{newName}";

        dir.DirectoryName = newName;
        dir.NormalizedPath = newPath;
        dir.UpdatedAtUtc = DateTime.UtcNow;

        await UpdateNestedPathsAsync(oldPath, newPath, ownerId, ct);

        await _db.SaveChangesAsync(ct);
        return dir;
    }

    public async Task<StorageDirectory?> MoveDirectoryAsync(Guid directoryId, Guid ownerId, Guid? newParentId, string newNormalizedPath, CancellationToken ct = default)
    {
        var dir = await _db.Directories.FirstOrDefaultAsync(d => d.DirectoryId == directoryId && d.OwnerId == ownerId, ct);
        if (dir == null) return null;

        string oldPath = dir.NormalizedPath;
        dir.ParentDirectoryId = newParentId;
        dir.NormalizedPath = newNormalizedPath;
        dir.UpdatedAtUtc = DateTime.UtcNow;

        await UpdateNestedPathsAsync(oldPath, newNormalizedPath, ownerId, ct);

        await _db.SaveChangesAsync(ct);
        return dir;
    }

    private async Task UpdateNestedPathsAsync(string oldPrefix, string newPrefix, Guid ownerId, CancellationToken ct)
    {
        var subdirs = await _db.Directories.Where(d => d.OwnerId == ownerId && d.NormalizedPath.StartsWith(oldPrefix + "/")).ToListAsync(ct);
        foreach (var sub in subdirs)
        {
            sub.NormalizedPath = newPrefix + sub.NormalizedPath[oldPrefix.Length..];
            sub.UpdatedAtUtc = DateTime.UtcNow;
        }

        var files = await _db.Files.Where(f => f.OwnerId == ownerId && f.NormalizedPath.StartsWith(oldPrefix + "/")).ToListAsync(ct);
        foreach (var file in files)
        {
            file.NormalizedPath = newPrefix + file.NormalizedPath[oldPrefix.Length..];
            file.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public async Task<IReadOnlyList<StorageDirectory>> GetSubdirectoriesAsync(Guid ownerId, Guid? parentDirectoryId, CancellationToken ct = default)
    {
        return await _db.Directories
            .Where(d => d.OwnerId == ownerId && d.ParentDirectoryId == parentDirectoryId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<object>> ListDirectoryContentsAsync(Guid ownerId, Guid directoryId, bool recursive, CancellationToken ct = default)
    {
        var result = new List<object>();

        var subdirs = await _db.Directories.Where(d => d.OwnerId == ownerId && d.ParentDirectoryId == directoryId).ToListAsync(ct);
        result.AddRange(subdirs);

        var files = await _db.Files.Where(f => f.OwnerId == ownerId && f.ParentDirectoryId == directoryId).ToListAsync(ct);
        result.AddRange(files);

        if (recursive)
        {
            foreach (var sub in subdirs)
            {
                var nested = await ListDirectoryContentsAsync(ownerId, sub.DirectoryId, true, ct);
                result.AddRange(nested);
            }
        }

        return result;
    }
}
