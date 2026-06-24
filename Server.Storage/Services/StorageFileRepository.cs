using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Interfaces;
using Server.Storage.Models;

namespace Server.Storage.Services;

public class StorageFileRepository : IStorageFileRepository
{
    private readonly StorageDbContext _db;

    public StorageFileRepository(StorageDbContext db)
    {
        _db = db;
    }

    public async Task<StorageFile?> GetFileAsync(Guid fileId, Guid ownerId, CancellationToken ct = default)
    {
        return await _db.Files
            .Include(f => f.Metadata)
            .FirstOrDefaultAsync(f => f.FileId == fileId && f.OwnerId == ownerId, ct);
    }

    public async Task<StorageFile?> GetFileByPathAsync(Guid ownerId, string normalizedPath, CancellationToken ct = default)
    {
        return await _db.Files
            .Include(f => f.Metadata)
            .FirstOrDefaultAsync(f => f.OwnerId == ownerId && f.NormalizedPath == normalizedPath, ct);
    }

    public async Task<StorageFile> CreateFileAsync(StorageFile file, CancellationToken ct = default)
    {
        file.FileId = Guid.NewGuid();
        file.CreatedAtUtc = DateTime.UtcNow;
        _db.Files.Add(file);
        await _db.SaveChangesAsync(ct);
        return file;
    }

    public async Task<StorageFile?> UpdateFileAsync(StorageFile file, CancellationToken ct = default)
    {
        var existing = await _db.Files.FirstOrDefaultAsync(f => f.FileId == file.FileId && f.OwnerId == file.OwnerId, ct);
        if (existing == null) return null;

        existing.FileName = file.FileName;
        existing.NormalizedPath = file.NormalizedPath;
        existing.ParentDirectoryId = file.ParentDirectoryId;
        existing.ContentHash = file.ContentHash;
        existing.Size = file.Size;
        existing.BucketName = file.BucketName;
        existing.StorageKey = file.StorageKey;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteFileAsync(Guid fileId, Guid ownerId, CancellationToken ct = default)
    {
        var file = await _db.Files.FirstOrDefaultAsync(f => f.FileId == fileId && f.OwnerId == ownerId, ct);
        if (file == null) return false;

        file.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<StorageFile?> MoveFileAsync(Guid fileId, Guid ownerId, Guid? newParentId, string newNormalizedPath, CancellationToken ct = default)
    {
        var file = await _db.Files.FirstOrDefaultAsync(f => f.FileId == fileId && f.OwnerId == ownerId, ct);
        if (file == null) return null;

        file.ParentDirectoryId = newParentId;
        file.NormalizedPath = newNormalizedPath;
        file.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return file;
    }

    public async Task<StorageFile?> CopyFileAsync(Guid sourceFileId, Guid ownerId, Guid? newParentId, string newNormalizedPath, CancellationToken ct = default)
    {
        var source = await _db.Files.Include(f => f.Metadata).FirstOrDefaultAsync(f => f.FileId == sourceFileId && f.OwnerId == ownerId, ct);
        if (source == null) return null;

        var copy = new StorageFile
        {
            FileId = Guid.NewGuid(),
            OwnerId = ownerId,
            ParentDirectoryId = newParentId,
            FileName = source.FileName,
            NormalizedPath = newNormalizedPath,
            Size = source.Size,
            StorageKey = source.StorageKey,
            BucketName = source.BucketName,
            CreatedAtUtc = DateTime.UtcNow,
            ContentHash = source.ContentHash,
            Metadata = source.Metadata.Select(m => new FileMetadataEntry
            {
                Id = Guid.NewGuid(),
                Key = m.Key,
                Value = m.Value
            }).ToList()
        };

        _db.Files.Add(copy);
        await _db.SaveChangesAsync(ct);
        return copy;
    }

    public async Task<StorageFile?> RenameFileAsync(Guid fileId, Guid ownerId, string newName, CancellationToken ct = default)
    {
        var file = await _db.Files.FirstOrDefaultAsync(f => f.FileId == fileId && f.OwnerId == ownerId, ct);
        if (file == null) return null;

        string parentPath = Path.GetDirectoryName(file.NormalizedPath)?.Replace('\\', '/') ?? "/";
        if (!parentPath.StartsWith("/")) parentPath = "/" + parentPath;

        file.FileName = newName;
        file.NormalizedPath = parentPath == "/" ? $"/{newName}" : $"{parentPath}/{newName}";
        file.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return file;
    }

    public async Task<IReadOnlyList<StorageFile>> ListFilesAsync(Guid ownerId, Guid? directoryId, bool recursive, CancellationToken ct = default)
    {
        if (directoryId == null)
        {
            return await _db.Files
                .Where(f => f.OwnerId == ownerId && f.ParentDirectoryId == null)
                .ToListAsync(ct);
        }

        if (!recursive)
        {
            return await _db.Files
                .Where(f => f.OwnerId == ownerId && f.ParentDirectoryId == directoryId)
                .ToListAsync(ct);
        }

        var dir = await _db.Directories.FirstOrDefaultAsync(d => d.DirectoryId == directoryId && d.OwnerId == ownerId, ct);
        if (dir == null) return Array.Empty<StorageFile>();

        string prefix = dir.NormalizedPath.TrimEnd('/') + "/";
        return await _db.Files
            .Where(f => f.OwnerId == ownerId && f.NormalizedPath.StartsWith(prefix))
            .ToListAsync(ct);
    }
}
