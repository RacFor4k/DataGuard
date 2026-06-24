using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Interfaces;
using Server.Storage.Models;

namespace Server.Storage.Services;

public class StorageMetadataService : IStorageMetadataService
{
    private readonly StorageDbContext _db;
    private const int MaxKeyLength = 256;
    private const int MaxValueLength = 4096;
    private const int MaxKeysPerFile = 64;
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "storagekey", "ownerid", "physicalpath", "bucketname"
    };

    public StorageMetadataService(StorageDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, string>> GetMetadataAsync(Guid fileId, CancellationToken ct = default)
    {
        var entries = await _db.FileMetadataEntries
            .Where(m => m.FileId == fileId)
            .ToListAsync(ct);

        return entries.ToDictionary(e => e.Key, e => e.Value);
    }

    public async Task UpdateMetadataAsync(Guid fileId, Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        var existing = await _db.FileMetadataEntries.Where(m => m.FileId == fileId).ToListAsync(ct);

        if (metadata.Count > MaxKeysPerFile)
            throw new InvalidOperationException("Превышено максимальное количество ключей метаданных.");

        foreach (var kvp in metadata)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                throw new ArgumentException("Ключ метаданных не может быть пустым.");

            if (kvp.Key.Length > MaxKeyLength)
                throw new ArgumentException("Превышена максимальная длина ключа метаданных.");

            if (kvp.Value.Length > MaxValueLength)
                throw new ArgumentException("Превышена максимальная длина значения метаданных.");

            if (ReservedKeys.Contains(kvp.Key) || kvp.Key.StartsWith("__"))
                throw new ArgumentException($"Ключ метаданных '{kvp.Key}' зарезервирован.");
        }

        _db.FileMetadataEntries.RemoveRange(existing);

        foreach (var kvp in metadata)
        {
            _db.FileMetadataEntries.Add(new FileMetadataEntry
            {
                Id = Guid.NewGuid(),
                FileId = fileId,
                Key = kvp.Key,
                Value = kvp.Value
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
