using System.Security.Claims;
using Contracts.Protos.CompanyManager;
using StackExchange.Redis;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Interfaces;
using Common.Server.Models;
using Server.Auth.Services;

namespace Server.Auth.Tests;

public class CompanyManagerServiceAdditionalTests
{
    // --- CreateCompany Tests ---

    [Fact]
    public async Task CreateCompany_EmptyNonceToken_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var response = await service.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "",
            MasterKey = ByteString.CopyFrom([0, 0, 0, 0, 1, 2, 3, 4]),
            CompanyName = "DataGuard",
            CompanyEmail = "owner@dg.local"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Nonce token is empty");
    }

    [Fact]
    public async Task CreateCompany_EmptyMasterKey_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var response = await service.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "nonce",
            MasterKey = ByteString.Empty,
            CompanyName = "DataGuard",
            CompanyEmail = "owner@dg.local"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Master key is empty");
    }

    [Fact]
    public async Task CreateCompany_EmptyCompanyName_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var response = await service.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "nonce",
            MasterKey = ByteString.CopyFrom([0, 0, 0, 0, 1, 2, 3, 4]),
            CompanyName = "",
            CompanyEmail = "owner@dg.local"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Company name is empty");
    }

    [Fact]
    public async Task CreateCompany_EmptyCompanyEmail_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var response = await service.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "nonce",
            MasterKey = ByteString.CopyFrom([0, 0, 0, 0, 1, 2, 3, 4]),
            CompanyName = "DataGuard",
            CompanyEmail = ""
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Company email is empty");
    }

    [Fact]
    public async Task CreateCompany_InvalidNonceToken_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("bad-nonce")).ReturnsAsync(false);
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var response = await service.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "bad-nonce",
            MasterKey = ByteString.CopyFrom([0, 0, 0, 0, 1, 2, 3, 4]),
            CompanyName = "DataGuard",
            CompanyEmail = "owner@dg.local"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Nonce token is invalid");
    }

    [Fact]
    public async Task CreateCompany_InvalidMasterKeyHash_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("nonce")).ReturnsAsync(true);
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([9, 9, 9, 9]),
            db, multiplexer.Object, new UserAccessor());

        // MasterKey = salt (4 bytes) + hash (4 bytes), hash = [0,0,0,0] != [9,9,9,9]
        var response = await service.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "nonce",
            MasterKey = ByteString.CopyFrom([0, 0, 0, 0, 0, 0, 0, 0]),
            CompanyName = "DataGuard",
            CompanyEmail = "owner@dg.local"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Master key is invalid");
    }

    [Fact]
    public async Task CreateCompany_Success_CreatesSystemOwnerGroup()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .ReturnsAsync(true);
        redisDb
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        redisDb
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("nonce")).ReturnsAsync(true);
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var response = await service.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "nonce",
            MasterKey = ByteString.CopyFrom([9, 8, 7, 6, 1, 2, 3, 4]),
            CompanyName = "DataGuard",
            CompanyEmail = "owner@dg.local"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.Message.Should().Be("OK");
        response.RegistrationCode.Should().NotBeNullOrEmpty();

        var company = await db.Companies.Include(c => c.Groups).FirstOrDefaultAsync();
        company.Should().NotBeNull();
        company!.Name.Should().Be("DataGuard");
        company.Groups.Should().Contain(g => g.Name == "system:owner");
    }

    // --- GetCompanyPublicKey Tests ---

    [Fact]
    public async Task GetCompanyPublicKey_EmptyCode_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var response = await service.GetCompanyPublicKey(new GetCompanyPublicKeyRequest { RegistrationCode = "" },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Registration code is empty");
    }

    [Fact]
    public async Task GetCompanyPublicKey_InvalidCode_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var response = await service.GetCompanyPublicKey(new GetCompanyPublicKeyRequest { RegistrationCode = "INVALID" },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Registration code is invalid");
    }

    [Fact]
    public async Task GetCompanyPublicKey_NullCompanyKey_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                redisStore.TryGetValue(key.ToString(), out var v) ? (RedisValue)v : RedisValue.Null);
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        // Create a company WITHOUT public key
        var company = new Company { Name = "NoKeyCo" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        redisStore["auth:REGCODE"] = System.Text.Json.JsonSerializer.Serialize(new
        {
            CompanyId = company.CompanyId, Name = "Test", Surname = "", Email = "t@t.com",
            Groups = Array.Empty<Guid>(), AdminGroups = Array.Empty<Guid>()
        });

        var response = await service.GetCompanyPublicKey(new GetCompanyPublicKeyRequest { RegistrationCode = "REGCODE" },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Company is invalid");
    }

    [Fact]
    public async Task GetCompanyPublicKey_Success_ReturnsKey()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                redisStore.TryGetValue(key.ToString(), out var v) ? (RedisValue)v : RedisValue.Null);
        var security = new Mock<ISecurityService>();
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, new UserAccessor());

        var company = new Company { Name = "KeyCo", PublicKeyPem = "MIIB pubkey here" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        redisStore["auth:GOODCODE"] = System.Text.Json.JsonSerializer.Serialize(new
        {
            CompanyId = company.CompanyId, Name = "Test", Surname = "", Email = "t@t.com",
            Groups = Array.Empty<Guid>(), AdminGroups = Array.Empty<Guid>()
        });

        var response = await service.GetCompanyPublicKey(new GetCompanyPublicKeyRequest { RegistrationCode = "GOODCODE" },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.CompanyPublicKeyPem.Should().Be("MIIB pubkey here");
    }

    // --- SetCompanyPublicKey Tests ---

    [Fact]
    public async Task SetCompanyPublicKey_NoAuth_Returns403()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor(); // UserJwt is null
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, userAccessor);

        var response = await service.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = "CODE",
            CompanyPublicKeyPem = "key-pem"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(403);
    }

    [Fact]
    public async Task SetCompanyPublicKey_NotAccessToken_Returns403()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims:
            [
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Typ, "refresh"),
            ])
        };
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, userAccessor);

        var response = await service.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = "CODE",
            CompanyPublicKeyPem = "key-pem"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(403);
    }

    [Fact]
    public async Task SetCompanyPublicKey_NoOwnerRole_Returns403()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = CreateAccessToken(Guid.NewGuid(), ["admin"])
        };
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, userAccessor);

        var ctx = TestSupport.CreateServerCallContextWithClaims(new Claim("role", "admin"));
        var response = await service.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = "CODE",
            CompanyPublicKeyPem = "key-pem"
        }, ctx);

        response.Status.Should().Be(403);
    }

    [Fact]
    public async Task SetCompanyPublicKey_EmptyRegistrationCode_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor { UserJwt = CreateAccessToken(Guid.NewGuid(), ["system:owner"]) };
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, userAccessor);

        var ctx = TestSupport.CreateServerCallContextWithClaims(new Claim("role", "system:owner"));
        var response = await service.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = "",
            CompanyPublicKeyPem = "key-pem"
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Be("Registration code is empty");
    }

    [Fact]
    public async Task SetCompanyPublicKey_EmptyPublicKey_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor { UserJwt = CreateAccessToken(Guid.NewGuid(), ["system:owner"]) };
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, userAccessor);

        var ctx = TestSupport.CreateServerCallContextWithClaims(new Claim("role", "system:owner"));
        var response = await service.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = "CODE",
            CompanyPublicKeyPem = ""
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Be("Company public key is empty");
    }

    [Fact]
    public async Task SetCompanyPublicKey_InvalidRegistrationCode_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor { UserJwt = CreateAccessToken(Guid.NewGuid(), ["system:owner"]) };
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, userAccessor);

        var ctx = TestSupport.CreateServerCallContextWithClaims(new Claim("role", "system:owner"));
        var response = await service.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = "INVALID",
            CompanyPublicKeyPem = "key-pem"
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Be("Registration code is invalid");
    }

    [Fact]
    public async Task SetCompanyPublicKey_CompanyNotFound_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                redisStore.TryGetValue(key.ToString(), out var v) ? (RedisValue)v : RedisValue.Null);
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor { UserJwt = CreateAccessToken(Guid.NewGuid(), ["system:owner"]) };
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, userAccessor);

        // Store registration data pointing to a non-existent company
        var phantomCompanyId = Guid.NewGuid();
        redisStore["auth:MYCODE"] = System.Text.Json.JsonSerializer.Serialize(new
        {
            CompanyId = phantomCompanyId, Name = "Test", Surname = "", Email = "t@t.com",
            Groups = Array.Empty<Guid>(), AdminGroups = Array.Empty<Guid>()
        });

        var ctx = TestSupport.CreateServerCallContextWithClaims(new Claim("role", "system:owner"));
        var response = await service.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = "MYCODE",
            CompanyPublicKeyPem = "key-pem"
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Be("Company is invalid");
    }

    [Fact]
    public async Task SetCompanyPublicKey_Success_SavesKeyToDb()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                redisStore.TryGetValue(key.ToString(), out var v) ? (RedisValue)v : RedisValue.Null);
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor { UserJwt = CreateAccessToken(Guid.NewGuid(), ["system:owner"]) };
        var service = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance, security.Object,
            TestSupport.CreateSecurityOptions(), TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db, multiplexer.Object, userAccessor);

        var company = new Company { Name = "KeyCo" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        redisStore["auth:SETKEY"] = System.Text.Json.JsonSerializer.Serialize(new
        {
            CompanyId = company.CompanyId, Name = "Test", Surname = "", Email = "t@t.com",
            Groups = Array.Empty<Guid>(), AdminGroups = Array.Empty<Guid>()
        });

        var ctx = TestSupport.CreateServerCallContextWithClaims(new Claim("role", "system:owner"));
        var response = await service.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = "SETKEY",
            CompanyPublicKeyPem = "NEW-PUBLIC-KEY"
        }, ctx);

        response.Status.Should().Be(200);
        response.Message.Should().Be("OK");

        var updated = await db.Companies.FindAsync(company.CompanyId);
        updated!.PublicKeyPem.Should().Be("NEW-PUBLIC-KEY");
    }

    private static System.IdentityModel.Tokens.Jwt.JwtSecurityToken CreateAccessToken(Guid userId, string[] roles)
    {
        var claims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Typ, "access"),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId.ToString()),
        };
        foreach (var role in roles)
            claims.Add(new Claim("role", role));
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims: claims);
    }
}