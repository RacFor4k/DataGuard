using System.Security.Cryptography;
using System.Text.Json;
using Contracts.Protos.Auth;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Interfaces;
using Common.Server.Models;
using Server.Auth.Models;
using Server.Auth.Services;
using StackExchange.Redis;

namespace Server.Auth.Tests;

public class AuthenticationServiceTests
{
    // --- Register Tests ---

    [Fact]
    public async Task Register_EmptyRegistrationCode_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var response = await service.Register(new RegisterRequest { RegistrationCode = "" },
            TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Registration code is empty");
    }

    [Fact]
    public async Task Register_InvalidEncryptedPasswordLength_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("{}");
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        // EncryptedPasswordLength(8) + NonceLength(2) + TagLength(2) = 12, send 5
        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "CODE12345678",
            EncryptedPassword = ByteString.CopyFrom(new byte[5]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Password is invalid");
    }

    [Fact]
    public async Task Register_InvalidEncryptedKeyLength_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("{}");
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "CODE12345678",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[3]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Key is invalid");
    }

    [Fact]
    public async Task Register_InvalidPasswordHashLength_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("{}");
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "CODE12345678",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[2]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Password hash is invalid");
    }

    [Fact]
    public async Task Register_InvalidClientSaltLength_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("{}");
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "CODE12345678",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[99]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Client salt is invalid");
    }

    [Fact]
    public async Task Register_InvalidBackupEncryptedKeyLength_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("{}");
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "CODE12345678",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[1]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Backup encrypted key is invalid");
    }

    [Fact]
    public async Task Register_RegistrationCodeNotFoundInRedis_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "NOTFOUND12345",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Registration code is invalid");
    }

    [Fact]
    public async Task Register_RegistrationDataDeserializationFailure_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("null");
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "BADJSON123456",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Registration data is invalid");
    }

    [Fact]
    public async Task Register_CompanyNotFound_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                redisStore.TryGetValue(key.ToString(), out var v) ? v : RedisValue.Null);
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var nonExistentCompanyId = Guid.NewGuid();
        var regData = new RegistrationData
        {
            CompanyId = nonExistentCompanyId,
            Name = "Test",
            Surname = "User",
            Email = "test@example.com",
            Groups = []
        };
        redisStore["auth:CODE12345678"] = JsonSerializer.Serialize(regData);

        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "CODE12345678",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Company is invalid");
    }

    [Fact]
    public async Task Register_CompanyHasNullPublicKeyPem_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                redisStore.TryGetValue(key.ToString(), out var v) ? v : RedisValue.Null);
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.GenerateSalt()).Returns([1, 2, 3, 4]);
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var company = new Company { Name = "TestCo" };
        company.Groups.Add(new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId
        });
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var regData = new RegistrationData
        {
            CompanyId = company.CompanyId,
            Name = "Test",
            Surname = "User",
            Email = "test@example.com",
            Groups = company.Groups.Select(g => g.Id).ToList(),
        };
        redisStore["auth:REGCODE12345"] = JsonSerializer.Serialize(regData);

        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "REGCODE12345",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Company is invalid");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                redisStore.TryGetValue(key.ToString(), out var v) ? v : RedisValue.Null);
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var company = new Company { Name = "TestCo", PublicKeyPem = "key-pem" };
        company.Groups.Add(new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId
        });
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var existingUser = new User
        {
            Name = "Existing", Surname = "User", Email = "dup@example.com",
            EncryptedPassword = [1], EncryptedKey = [1], ServerPasswordHash = [1],
            ClientSalt = [1], ServerSalt = [1], CompanyId = company.CompanyId, Company = company
        };
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        var regData = new RegistrationData
        {
            CompanyId = company.CompanyId,
            Name = "New", Surname = "User", Email = "dup@example.com",
            Groups = company.Groups.Select(g => g.Id).ToList()
        };
        redisStore["auth:REGCODE99999"] = JsonSerializer.Serialize(regData);

        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "REGCODE99999",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(409);
        response.Message.Should().Be("User is already registered");
    }

    [Fact]
    public async Task Register_Success_Returns200WithAllFields()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                redisStore.TryGetValue(key.ToString(), out var v) ? v : RedisValue.Null);
        redisDb
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => redisStore.Remove(key.ToString()))
            .ReturnsAsync(true);
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("access-token");
        jwt.Setup(j => j.GenerateRefreshToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("refresh-token");
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.GenerateSalt()).Returns([5, 6, 7, 8]);
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var company = new Company { Name = "TestCo", PublicKeyPem = "pub-key-pem" };
        company.Groups.Add(new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId
        });
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var regData = new RegistrationData
        {
            CompanyId = company.CompanyId,
            Name = "Иван", Surname = "Иванов", Email = "ivan@test.com",
            Groups = company.Groups.Select(g => g.Id).ToList()
        };
        redisStore["auth:GOODREG12345"] = JsonSerializer.Serialize(regData);

        var response = await service.Register(new RegisterRequest
        {
            RegistrationCode = "GOODREG12345",
            EncryptedPassword = ByteString.CopyFrom(new byte[opts.EncryptedPasswordLength + opts.NonceLength + opts.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[opts.EncryptedKeyLength + opts.NonceLength + opts.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[opts.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(new byte[opts.PasswordHashLength + opts.SaltLength]),
            ClientSalt = ByteString.CopyFrom(new byte[opts.SaltLength]),
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.Message.Should().Be("OK");
        response.UserId.Should().NotBeNullOrEmpty();
        response.Email.Should().Be("ivan@test.com");
        response.CompanyPublicKeyPem.Should().Be("pub-key-pem");
        response.JwtAccessToken.Should().Be("access-token");
        response.JwtRefreshToken.Should().Be("refresh-token");
    }

    // --- Login Tests ---

    [Fact]
    public async Task Login_EmptyUserId_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Login(new LoginRequest
        {
            UserId = "",
            PasswordHash = ByteString.CopyFrom(new byte[opts.SaltLength + opts.PasswordHashLength]),
            NonceToken = "nonce"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("UserId is empty");
    }

    [Fact]
    public async Task Login_InvalidPasswordHashLength_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var response = await service.Login(new LoginRequest
        {
            UserId = Guid.NewGuid().ToString(),
            PasswordHash = ByteString.CopyFrom(new byte[1]),
            NonceToken = "nonce"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Password is invalid");
    }

    [Fact]
    public async Task Login_EmptyNonceToken_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Login(new LoginRequest
        {
            UserId = Guid.NewGuid().ToString(),
            PasswordHash = ByteString.CopyFrom(new byte[opts.SaltLength + opts.PasswordHashLength]),
            NonceToken = ""
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("NonceToken is empty");
    }

    [Fact]
    public async Task Login_InvalidNonceToken_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("bad-nonce")).ReturnsAsync(false);
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Login(new LoginRequest
        {
            UserId = Guid.NewGuid().ToString(),
            PasswordHash = ByteString.CopyFrom(new byte[opts.SaltLength + opts.PasswordHashLength]),
            NonceToken = "bad-nonce"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("NonceToken is invalid");
    }

    [Fact]
    public async Task Login_NonExistentUser_Returns401_AndExecutesDummyHash()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("nonce")).ReturnsAsync(true);
        security.Setup(s => s.HashPasswordAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>()))
            .ReturnsAsync(new byte[4]);
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var response = await service.Login(new LoginRequest
        {
            UserId = Guid.NewGuid().ToString(),
            PasswordHash = ByteString.CopyFrom(new byte[opts.SaltLength + opts.PasswordHashLength]),
            NonceToken = "nonce"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(401);
        // Verify that dummy hash was executed for timing attack prevention
        security.Verify(s => s.HashPasswordAsync(
            It.IsAny<byte[]>(),
            It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task Login_WrongPasswordHash_Returns401()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("nonce")).ReturnsAsync(true);
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var company = new Company { Name = "Co" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var user = new User
        {
            Name = "Test", Surname = "User", Email = "test@example.com",
            EncryptedPassword = [1], EncryptedKey = [1],
            ServerPasswordHash = [10, 10, 10, 10],
            ClientSalt = [1, 2, 3, 4], ServerSalt = [5, 6, 7, 8],
            CompanyId = company.CompanyId, Company = company
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Send wrong hash (all zeros for the hash portion)
        var wrongHash = new byte[opts.SaltLength + opts.PasswordHashLength];
        var response = await service.Login(new LoginRequest
        {
            UserId = user.UserId.ToString(),
            PasswordHash = ByteString.CopyFrom(wrongHash),
            NonceToken = "nonce"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(401);
    }

    [Fact]
    public async Task Login_Success_Returns200WithEncryptedKeyAndTokens()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("access-token");
        jwt.Setup(j => j.GenerateRefreshToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("refresh-token");
        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("nonce")).ReturnsAsync(true);
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), new UserAccessor());

        var opts = TestSupport.CreateSecurityOptions().Value;
        var company = new Company { Name = "Co" };
        company.Groups.Add(new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId
        });
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var serverHash = new byte[opts.PasswordHashLength]; // all zeros
        var clientSalt = new byte[opts.SaltLength]; // all zeros
        var user = new User
        {
            Name = "Test", Surname = "User", Email = "test@example.com",
            EncryptedPassword = [1], EncryptedKey = [2, 2, 2, 2],
            ServerPasswordHash = serverHash,
            ClientSalt = clientSalt, ServerSalt = [5, 6, 7, 8],
            CompanyId = company.CompanyId, Company = company
        };
        // Add group membership
        var groupMember = new GroupMember
        {
            GroupId = company.Groups.First().Id,
            Group = company.Groups.First(),
            UserId = user.UserId,
            User = user,
            CompanyId = company.CompanyId,
            JoinDate = DateTime.UtcNow,
            Role = GroupRole.User
        };
        db.Users.Add(user);
        db.GroupMembers.Add(groupMember);
        await db.SaveChangesAsync();

        // Build password hash with correct client salt prefix + matching server hash
        var loginHash = new byte[opts.SaltLength + opts.PasswordHashLength];
        // loginHash has matching serverHash portion
        var response = await service.Login(new LoginRequest
        {
            UserId = user.UserId.ToString(),
            PasswordHash = ByteString.CopyFrom(loginHash),
            NonceToken = "nonce"
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.Message.Should().Be("OK");
        response.JwtAccessToken.Should().Be("access-token");
        response.JwtRefreshToken.Should().Be("refresh-token");
        response.EncryptedKey.Should().BeEquivalentTo([2, 2, 2, 2]);
    }

    // --- RefreshToken Tests ---

    [Fact]
    public async Task RefreshToken_NoUserJwt_Returns401()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var response = await service.RefreshToken(new RefreshTokenRequest(), TestSupport.CreateServerCallContext());

        response.Status.Should().Be(401);
        response.Message.Should().Be("Token is invalid");
    }

    [Fact]
    public async Task RefreshToken_UserJwtIsAccessToken_Returns401()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims:
            [
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Typ, "access"),
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            ])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var response = await service.RefreshToken(new RefreshTokenRequest(), TestSupport.CreateServerCallContext());

        response.Status.Should().Be(401);
        response.Message.Should().Be("Token is invalid");
    }

    [Fact]
    public async Task RefreshToken_ValidRefreshToken_Returns200WithNewTokens()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var userId = Guid.NewGuid();
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("new-access-token");
        jwt.Setup(j => j.GenerateRefreshToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("new-refresh-token");
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims:
            [
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Typ, "refresh"),
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId.ToString()),
            ])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var company = new Company { Name = "Co" };
        company.Groups.Add(new Group
        {
            Name = "grp1",
            Company = company,
            CompanyId = company.CompanyId
        });
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var user = new User
        {
            UserId = userId,
            Name = "T", Surname = "U", Email = "t@t.com",
            EncryptedPassword = [1], EncryptedKey = [1],
            ServerPasswordHash = [1], ClientSalt = [1], ServerSalt = [1],
            CompanyId = company.CompanyId, Company = company
        };
        user.GroupMembers.Add(new GroupMember
        {
            GroupId = company.Groups.First().Id,
            Group = company.Groups.First(),
            UserId = user.UserId,
            User = user,
            CompanyId = company.CompanyId,
            JoinDate = DateTime.UtcNow,
            Role = GroupRole.User
        });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var response = await service.RefreshToken(new RefreshTokenRequest(), TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.Message.Should().Be("OK");
        response.JwtAccessToken.Should().Be("new-access-token");
        response.JwtRefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RefreshToken_NonExistentUser_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims:
            [
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Typ, "refresh"),
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            ])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var response = await service.RefreshToken(new RefreshTokenRequest(), TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Be("Token is invalid");
    }

    // --- CreateRegistrationCode Tests ---

    [Fact]
    public async Task CreateRegistrationCode_EmptyName_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = CreateAccessTokenWithRoles(Guid.NewGuid(), ["system:owner"])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var ctx = TestSupport.CreateServerCallContext();
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "",
            Surname = "Doe",
            Email = "test@test.com",
            Groups = { Guid.NewGuid().ToString() }
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Be("Name is empty");
    }

    [Fact]
    public async Task CreateRegistrationCode_EmptyEmail_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = CreateAccessTokenWithRoles(Guid.NewGuid(), ["system:owner"])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var ctx = TestSupport.CreateServerCallContext();
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "John",
            Surname = "Doe",
            Email = "",
            Groups = { Guid.NewGuid().ToString() }
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Be("Email is empty");
    }

    [Fact]
    public async Task CreateRegistrationCode_InvalidEmailFormat_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = CreateAccessTokenWithRoles(Guid.NewGuid(), ["system:owner"])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var ctx = TestSupport.CreateServerCallContext();
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "John",
            Surname = "Doe",
            Email = "not-an-email",
            Groups = { Guid.NewGuid().ToString() }
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Be("Email is invalid");
    }

    [Fact]
    public async Task CreateRegistrationCode_EmptyGroups_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = CreateAccessTokenWithRoles(Guid.NewGuid(), ["system:owner"])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var ctx = TestSupport.CreateServerCallContext();
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "John",
            Surname = "Doe",
            Email = "test@test.com",
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Be("Groups is empty");
    }

    [Fact]
    public async Task CreateRegistrationCode_NoUserJwt_Returns401()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor();
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var ctx = TestSupport.CreateServerCallContext();
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "John",
            Surname = "Doe",
            Email = "test@test.com",
            Groups = { Guid.NewGuid().ToString() }
        }, ctx);

        response.Status.Should().Be(401);
        response.Message.Should().Be("Token is invalid");
    }

    [Fact]
    public async Task CreateRegistrationCode_UserJwtIsNotAccessToken_Returns401()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims:
            [
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Typ, "refresh"),
            ])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var ctx = TestSupport.CreateServerCallContext();
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "John",
            Surname = "Doe",
            Email = "test@test.com",
            Groups = { Guid.NewGuid().ToString() }
        }, ctx);

        response.Status.Should().Be(401);
        response.Message.Should().Be("Token is invalid");
    }

    [Fact]
    public async Task CreateRegistrationCode_MissingSystemOwnerRole_Returns403()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _) = TestSupport.CreateRedisMock();
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = CreateAccessTokenWithRoles(Guid.NewGuid(), ["admin"])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var ctx = TestSupport.CreateServerCallContextWithClaims(new System.Security.Claims.Claim("role", "admin"));
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "John",
            Surname = "Doe",
            Email = "test@test.com",
            Groups = { Guid.NewGuid().ToString() }
        }, ctx);

        response.Status.Should().Be(403);
    }

    [Fact]
    public async Task CreateRegistrationCode_AdminGroupsNotSubsetOfGroups_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var userId = Guid.NewGuid();
        var (multiplexer, redisDb) = TestSupport.CreateRedisMock();
        redisDb
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .ReturnsAsync(true);
        redisDb
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = CreateAccessTokenWithRoles(userId, ["system:owner"])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var company = new Company { Name = "Co" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var user = new User
        {
            Name = "O", Surname = "W", Email = "o@o.com",
            EncryptedPassword = [1], EncryptedKey = [1],
            ServerPasswordHash = [1], ClientSalt = [1], ServerSalt = [1],
            CompanyId = company.CompanyId, Company = company
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var groupInList = Guid.NewGuid();
        var adminGroupNotInList = Guid.NewGuid();
        var ctx = TestSupport.CreateServerCallContextWithClaims(new System.Security.Claims.Claim("role", "system:owner"));
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "John",
            Surname = "Doe",
            Email = "test@test.com",
            Groups = { groupInList.ToString() },
            AdminGroups = { adminGroupNotInList.ToString() }
        }, ctx);

        response.Status.Should().Be(400);
        response.Message.Should().Contain("подмножеством");
    }

    [Fact]
    public async Task CreateRegistrationCode_Success_Returns200WithRegistrationCode()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var userId = Guid.NewGuid();
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
        var jwt = new Mock<IJwtService>();
        var security = new Mock<ISecurityService>();
        var userAccessor = new UserAccessor
        {
            UserJwt = CreateAccessTokenWithRoles(userId, ["system:owner"])
        };
        var service = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwt.Object, security.Object, TestSupport.CreateSecurityOptions(), userAccessor);

        var company = new Company { Name = "Co" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var user = new User
        {
            Name = "O", Surname = "W", Email = "o@o.com",
            EncryptedPassword = [1], EncryptedKey = [1],
            ServerPasswordHash = [1], ClientSalt = [1], ServerSalt = [1],
            CompanyId = company.CompanyId, Company = company
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var groupId = Guid.NewGuid();
        var ctx = TestSupport.CreateServerCallContextWithClaims(new System.Security.Claims.Claim("role", "system:owner"));
        var response = await service.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "John",
            Surname = "Doe",
            Email = "john@test.com",
            Groups = { groupId.ToString() },
            AdminGroups = { groupId.ToString() }
        }, ctx);

        response.Status.Should().Be(200);
        response.Message.Should().Be("OK");
        response.RegistrationCode.Should().NotBeNullOrEmpty();
        response.RegistrationCode.Should().HaveLength(12);
    }

    private static System.IdentityModel.Tokens.Jwt.JwtSecurityToken CreateAccessTokenWithRoles(
        Guid userId, string[] roles)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Typ, "access"),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId.ToString()),
        };
        foreach (var role in roles)
        {
            claims.Add(new System.Security.Claims.Claim("role", role));
        }
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims: claims);
    }
}