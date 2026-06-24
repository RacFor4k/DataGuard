using Contracts.Protos.Security;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Interfaces;
using Common.Server.Models;
using Server.Auth.Services;

namespace Server.Auth.Tests;

public class SecurityRequestsServiceTests
{
    [Fact]
    public async Task GetNonce_Returns200WithToken()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.GetNonceToken()).ReturnsAsync("nonce-token-123");
        var service = new SecurityRequestsService(security.Object, NullLogger<SecurityRequestsService>.Instance, db);

        var response = await service.GetNonce(new NonceRequest(), TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.Message.Should().Be("OK");
        response.NonceToken.Should().Be("nonce-token-123");
        security.Verify(s => s.GetNonceToken(), Times.Once);
    }

    [Fact]
    public async Task GetSalt_EmptyUserId_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var security = new Mock<ISecurityService>();
        var service = new SecurityRequestsService(security.Object, NullLogger<SecurityRequestsService>.Instance, db);

        var response = await service.GetSalt(new SaltRequest { UserId = "" },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("UserId is empty");
    }

    [Fact]
    public async Task GetSalt_InvalidUserIdFormat_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var security = new Mock<ISecurityService>();
        var service = new SecurityRequestsService(security.Object, NullLogger<SecurityRequestsService>.Instance, db);

        var response = await service.GetSalt(new SaltRequest { UserId = "not-a-guid" },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("UserId is invalid");
    }

    [Fact]
    public async Task GetSalt_UserNotFound_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var security = new Mock<ISecurityService>();
        var service = new SecurityRequestsService(security.Object, NullLogger<SecurityRequestsService>.Instance, db);

        var userId = Guid.NewGuid();
        var response = await service.GetSalt(new SaltRequest { UserId = userId.ToString() },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Client salt is invalid");
    }

    [Fact]
    public async Task GetSalt_Success_ReturnsSalt()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var security = new Mock<ISecurityService>();
        var service = new SecurityRequestsService(security.Object, NullLogger<SecurityRequestsService>.Instance, db);

        var opts = TestSupport.CreateSecurityOptions().Value;
        var company = new Company { Name = "Co" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var expectedSalt = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var user = new User
        {
            Name = "T", Surname = "U", Email = "t@t.com",
            EncryptedPassword = [1], EncryptedKey = [1],
            ServerPasswordHash = [1], ClientSalt = expectedSalt, ServerSalt = [1],
            CompanyId = company.CompanyId, Company = company
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var response = await service.GetSalt(new SaltRequest { UserId = user.UserId.ToString() },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.Message.Should().Be("OK");
        response.Salt.ToByteArray().Should().BeEquivalentTo(expectedSalt);
    }
}