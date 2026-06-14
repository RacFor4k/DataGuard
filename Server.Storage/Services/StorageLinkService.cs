using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Interfaces;
using Server.Storage.Models;

namespace Server.Storage.Services;

public class StorageLinkService : IStorageLinkService
{
    private readonly StorageDbContext _db;
    private const int MaxLinkTtlDays = 30;

    public StorageLinkService(StorageDbContext db)
    {
        _db = db;
    }

    public async Task<StorageSharedLink> GenerateLinkAsync(Guid fileId, Guid ownerId, TimeSpan ttl, bool isDirect, IEnumerable<string>? users, IEnumerable<string>? groups, CancellationToken ct = default)
    {
        if (ttl <= TimeSpan.Zero || ttl > TimeSpan.FromDays(MaxLinkTtlDays))
            throw new ArgumentException("TTL ссылки должен быть от 0 до 30 дней.");

        string token = GenerateSecureToken();

        var link = new StorageSharedLink
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            OwnerId = ownerId,
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.Add(ttl),
            IsDirect = isDirect,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.SharedLinks.Add(link);
        await _db.SaveChangesAsync(ct);
        return link;
    }

    public async Task<StorageSharedLink?> GetLinkAsync(string token, CancellationToken ct = default)
    {
        return await _db.SharedLinks
            .Include(l => l.File)
            .FirstOrDefaultAsync(l => l.Token == token, ct);
    }

    public async Task<bool> ValidateLinkAsync(string token, Guid? requestorId, CancellationToken ct = default)
    {
        var link = await GetLinkAsync(token, ct);
        if (link == null) return false;
        if (link.ExpiresAtUtc < DateTime.UtcNow) return false;
        return true;
    }

    private static string GenerateSecureToken()
    {
        byte[] bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
