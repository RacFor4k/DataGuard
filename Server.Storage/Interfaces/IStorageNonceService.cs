namespace Server.Storage.Interfaces;

public interface IStorageNonceService
{
    Task<bool> TryConsumeNonceAsync(Guid ownerId, string operationName, string nonceToken, TimeSpan ttl, CancellationToken ct = default);
}
