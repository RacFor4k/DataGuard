using Server.Storage.Models;

namespace Server.Storage.Interfaces;

public interface IStorageFileRepository
{
    Task<StorageFile?> GetFileAsync(Guid fileId, Guid ownerId, CancellationToken ct = default);
    Task<StorageFile?> GetFileByPathAsync(Guid ownerId, string normalizedPath, CancellationToken ct = default);
    Task<StorageFile> CreateFileAsync(StorageFile file, CancellationToken ct = default);
    Task<StorageFile?> UpdateFileAsync(StorageFile file, CancellationToken ct = default);
    Task<bool> DeleteFileAsync(Guid fileId, Guid ownerId, CancellationToken ct = default);
    Task<StorageFile?> MoveFileAsync(Guid fileId, Guid ownerId, Guid? newParentId, string newNormalizedPath, CancellationToken ct = default);
    Task<StorageFile?> CopyFileAsync(Guid sourceFileId, Guid ownerId, Guid? newParentId, string newNormalizedPath, CancellationToken ct = default);
    Task<StorageFile?> RenameFileAsync(Guid fileId, Guid ownerId, string newName, CancellationToken ct = default);
    Task<IReadOnlyList<StorageFile>> ListFilesAsync(Guid ownerId, Guid? directoryId, bool recursive, CancellationToken ct = default);
}
