using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Storage.Data;
using Server.Storage.Models;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StorageLinkServiceTests : IDisposable
{
    private readonly StorageDbContext _db;
    private readonly StorageLinkService _service;

    public StorageLinkServiceTests()
    {
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new StorageDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _service = new StorageLinkService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<StorageFile> SeedFile(Guid ownerId)
    {
        var file = new StorageFile
        {
            FileId = Guid.NewGuid(),
            OwnerId = ownerId,
            FileName = "test.txt",
            NormalizedPath = "/test.txt",
            Size = 100,
            StorageKey = "key1",
            BucketName = "dataguard-storage"
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();
        return file;
    }

    // ── GenerateLinkAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateLinkAsync_Success_ReturnsLinkWithToken()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);

        var link = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, null, null);

        link.Should().NotBeNull();
        link.Token.Should().NotBeNullOrEmpty();
        link.FileId.Should().Be(file.FileId);
        link.OwnerId.Should().Be(ownerId);
        link.IsDirect.Should().BeFalse();
        link.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task GenerateLinkAsync_TtlZero_ThrowsArgumentException()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);

        var act = () => _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.Zero, false, null, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateLinkAsync_TtlExceeds30Days_ThrowsArgumentException()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);

        var act = () => _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromDays(31), false, null, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateLinkAsync_WithUserRestrictions_SetsAllowedUsers()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var users = new List<string> { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

        var link = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, users, null);

        link.AllowedUsers.Should().BeEquivalentTo(users);
        link.AllowedGroups.Should().BeNull();
    }

    [Fact]
    public async Task GenerateLinkAsync_WithGroupRestrictions_SetsAllowedGroups()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var groups = new List<string> { "admins", "editors" };

        var link = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, null, groups);

        link.AllowedGroups.Should().BeEquivalentTo(groups);
        link.AllowedUsers.Should().BeNull();
    }

    [Fact]
    public async Task GenerateLinkAsync_IsDirect_SetsFlag()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);

        var link = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), true, null, null);

        link.IsDirect.Should().BeTrue();
    }

    // ── GetLinkAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLinkAsync_Found_ReturnsLink()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var created = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, null, null);

        var link = await _service.GetLinkAsync(created.Token);

        link.Should().NotBeNull();
        link!.Token.Should().Be(created.Token);
    }

    [Fact]
    public async Task GetLinkAsync_NotFound_ReturnsNull()
    {
        var link = await _service.GetLinkAsync("nonexistent-token-xyz");

        link.Should().BeNull();
    }

    // ── ValidateLinkAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateLinkAsync_ExpiredLink_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);

        // Create link in the past by inserting directly
        var link = new StorageSharedLink
        {
            Id = Guid.NewGuid(),
            FileId = file.FileId,
            OwnerId = ownerId,
            Token = "expired-token",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1),
            IsDirect = false
        };
        _db.SharedLinks.Add(link);
        await _db.SaveChangesAsync();

        var valid = await _service.ValidateLinkAsync("expired-token", null);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateLinkAsync_UnrestrictedLink_ReturnsTrue()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var created = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, null, null);

        var valid = await _service.ValidateLinkAsync(created.Token, null);

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateLinkAsync_OwnerAlwaysHasAccess_ReturnsTrue()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var otherUser = Guid.NewGuid().ToString();
        var users = new List<string> { otherUser };

        var created = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, users, null);

        var valid = await _service.ValidateLinkAsync(created.Token, ownerId);

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateLinkAsync_NonOwnerInAllowedUsers_ReturnsTrue()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var allowedUser = Guid.NewGuid();
        var users = new List<string> { allowedUser.ToString() };

        var created = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, users, null);

        var valid = await _service.ValidateLinkAsync(created.Token, allowedUser);

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateLinkAsync_NonOwnerNotInAllowedUsers_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var allowedUser = Guid.NewGuid();
        var randomUser = Guid.NewGuid();
        var users = new List<string> { allowedUser.ToString() };

        var created = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, users, null);

        var valid = await _service.ValidateLinkAsync(created.Token, randomUser);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateLinkAsync_NullRequestorIdWithRestrictions_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var users = new List<string> { Guid.NewGuid().ToString() };

        var created = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, users, null);

        var valid = await _service.ValidateLinkAsync(created.Token, null);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateLinkAsync_DeletedFile_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var file = await SeedFile(ownerId);
        var created = await _service.GenerateLinkAsync(file.FileId, ownerId, TimeSpan.FromHours(1), false, null, null);

        // Soft-delete the file
        file.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var valid = await _service.ValidateLinkAsync(created.Token, null);

        valid.Should().BeFalse();
    }
}