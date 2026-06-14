using Server.Storage.Models;

namespace Server.Storage.Interfaces;

public interface IStorageLinkService
{
    Task<StorageSharedLink> GenerateLinkAsync(Guid fileId, Guid ownerId, TimeSpan ttl, bool isDirect, IEnumerable<string>? users, IEnumerable<string>? groups, CancellationToken ct = default);
    Task<StorageSharedLink?> GetLinkAsync(string token, CancellationToken ct = default);
    Task<bool> ValidateLinkAsync(string token, Guid? requestorId, CancellationToken ct = default);
}
