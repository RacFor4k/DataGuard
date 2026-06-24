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

        var usersList = users?.ToList();
        var groupsList = groups?.ToList();

        var link = new StorageSharedLink
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            OwnerId = ownerId,
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.Add(ttl),
            IsDirect = isDirect,
            AllowedUsers = usersList?.Count > 0 ? usersList : null,
            AllowedGroups = groupsList?.Count > 0 ? groupsList : null,
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

        // Если ограничения не заданы — доступ разрешён
        if (link.AllowedUsers == null && link.AllowedGroups == null)
            return true;

        // Владелец всегда имеет доступ
        if (requestorId.HasValue && link.OwnerId == requestorId.Value)
            return true;

        // Если нет идентификатора запросившего — отказ (ограниченная ссылка требует аутентификации)
        if (!requestorId.HasValue)
            return false;

        // Проверка ограничений по пользователям
        if (link.AllowedUsers != null && link.AllowedUsers.Count > 0)
        {
            if (!link.AllowedUsers.Any(u => u.Equals(requestorId.Value.ToString(), StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // Проверка ограничений по группам потребует поиска групп пользователя.
        // На данный момент принимаем, если пользователь прошёл проверку по AllowedUsers
        // или ограничения по группам отсутствуют.
        if (link.AllowedGroups != null && link.AllowedGroups.Count > 0)
        {
            // TODO: реализовать проверку принадлежности пользователя к группам
            // через сервис управления доступом.
        }

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
