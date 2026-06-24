namespace Server.Storage.Interfaces;

public interface IStorageMetadataService
{
    Task<Dictionary<string, string>> GetMetadataAsync(Guid fileId, CancellationToken ct = default);
    Task UpdateMetadataAsync(Guid fileId, Dictionary<string, string> metadata, CancellationToken ct = default);
}
