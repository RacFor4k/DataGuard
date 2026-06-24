using Server.Storage.Interfaces;
using StackExchange.Redis;

namespace Server.Storage.Services;

public class StorageNonceService : IStorageNonceService
{
    private readonly IConnectionMultiplexer _redis;

    public StorageNonceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> TryConsumeNonceAsync(Guid ownerId, string operationName, string nonceToken, TimeSpan ttl, CancellationToken ct = default)
    {
        IDatabase db = _redis.GetDatabase();
        string key = $"nonce:{ownerId}:{operationName}:{nonceToken}";

        return await db.StringSetAsync(key, "1", ttl, When.NotExists);
    }
}
