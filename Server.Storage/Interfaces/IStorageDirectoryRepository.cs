using Server.Storage.Models;

namespace Server.Storage.Interfaces;

public interface IStorageDirectoryRepository
{
    Task<StorageDirectory?> GetDirectoryAsync(Guid directoryId, Guid ownerId, CancellationToken ct = default);
    Task<StorageDirectory?> GetDirectoryByPathAsync(Guid ownerId, string normalizedPath, CancellationToken ct = default);
    Task<StorageDirectory> CreateDirectoryAsync(StorageDirectory directory, CancellationToken ct = default);
    Task<bool> DeleteDirectoryAsync(Guid directoryId, Guid ownerId, bool recursive, CancellationToken ct = default);
    Task<StorageDirectory?> RenameDirectoryAsync(Guid directoryId, Guid ownerId, string newName, CancellationToken ct = default);
    Task<StorageDirectory?> MoveDirectoryAsync(Guid directoryId, Guid ownerId, Guid? newParentId, string newNormalizedPath, CancellationToken ct = default);
    Task<IReadOnlyList<StorageDirectory>> GetSubdirectoriesAsync(Guid ownerId, Guid? parentDirectoryId, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListDirectoryContentsAsync(Guid ownerId, Guid directoryId, bool recursive, CancellationToken ct = default);
}
