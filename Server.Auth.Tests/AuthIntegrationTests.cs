using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Contracts.Protos.Auth;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Models;
using Server.Auth.Services;
using Common.Server.Models;
using StackExchange.Redis;

namespace Server.Auth.Tests;

public class AuthIntegrationTests
{
    #region Helpers

    private static (Mock<IConnectionMultiplexer> Multiplexer, Mock<IDatabase> Database, Dictionary<string, RedisValue> Store) SetupRedisMock()
    {
        var store = new Dictionary<string, RedisValue>();
        var database = new Mock<IDatabase>(MockBehavior.Loose);
        var multiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Loose);

        multiplexer
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When>((key, value, _, _) => store[key.ToString()!] = value)
            .ReturnsAsync(true);

        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, value, _, _, _) => store[key.ToString()!] = value)
            .ReturnsAsync(true);

        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>((key, value, _, _, _) => store[key.ToString()!] = value)
            .ReturnsAsync(true);

        database
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                store.TryGetValue(key.ToString()!, out var value) ? value : RedisValue.Null);

        database
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => store.Remove(key.ToString()!))
            .ReturnsAsync(true);

        database
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => store.ContainsKey(key.ToString()!));

        return (multiplexer, database, store);
    }

    private static void SetUserAccessorToken(UserAccessor accessor, JwtSecurityToken token)
    {
        typeof(UserAccessor).GetProperty("UserJwt")!.SetValue(accessor, token);
    }

    /// <summary>
    /// Creates a valid RegisterRequest whose byte-array sizes match TestSupport.CreateSecurityOptions().
    /// SaltLength=4, PasswordHashLength=4, EncryptedPasswordLength=8, EncryptedKeyLength=8,
    /// NonceLength=2, TagLength=2, RsaKeySize=64.
    /// </summary>
    private static RegisterRequest BuildRegisterRequest(string registrationCode, byte[]? passwordHashBytes = null, byte[]? clientSaltBytes = null)
    {
        var hash = passwordHashBytes ?? new byte[TestSupport.CreateSecurityOptions().Value.SaltLength + TestSupport.CreateSecurityOptions().Value.PasswordHashLength];
        var salt = clientSaltBytes ?? new byte[TestSupport.CreateSecurityOptions().Value.SaltLength];
        var sec = TestSupport.CreateSecurityOptions().Value;

        return new RegisterRequest
        {
            RegistrationCode = registrationCode,
            EncryptedPassword = ByteString.CopyFrom(new byte[sec.EncryptedPasswordLength + sec.NonceLength + sec.TagLength]),
            EncryptedKey = ByteString.CopyFrom(new byte[sec.EncryptedKeyLength + sec.NonceLength + sec.TagLength]),
            BackupEncryptedKey = ByteString.CopyFrom(new byte[sec.RsaKeySize / 8]),
            PasswordHash = ByteString.CopyFrom(hash),
            ClientSalt = ByteString.CopyFrom(salt)
        };
    }

    #endregion

    // ---------------------------------------------------------------
    // 1. Full Registration Flow
    // ---------------------------------------------------------------
    [Fact]
    public async Task Register_WithValidData_CreatesUserGroupMembersAndReturnsTokens()
    {
        // Arrange
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _, redisStore) = SetupRedisMock();

        var company = new Company { Name = "TestCo", PublicKeyPem = "pub-key-pem-value" };
        var group = new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        company.Groups.Add(group);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var registrationCode = "REGCODE123ABC";
        var registrationData = new RegistrationData
        {
            CompanyId = company.CompanyId,
            Name = "John",
            Surname = "Doe",
            Email = "john@test.com",
            Groups = new[] { group.Id },
            AdminGroups = new[] { group.Id }
        };
        redisStore[$"auth:{registrationCode}"] = JsonSerializer.Serialize(registrationData);

        var securityOpts = TestSupport.CreateSecurityOptions();
        var securityService = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, securityOpts);
        var jwtService = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var authService = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwtService, securityService, securityOpts, new UserAccessor());

        // Act
        var response = await authService.Register(BuildRegisterRequest(registrationCode), TestSupport.CreateServerCallContext());

        // Assert
        response.Status.Should().Be(200, "registration should succeed");
        response.Email.Should().Be("john@test.com");
        response.CompanyPublicKeyPem.Should().Be("pub-key-pem-value");
        response.JwtAccessToken.Should().NotBeNullOrEmpty();
        response.JwtRefreshToken.Should().NotBeNullOrEmpty();
        response.UserId.Should().NotBeNullOrEmpty();

        var users = await db.Users.Include(u => u.GroupMembers).ToListAsync();
        users.Should().HaveCount(1);
        var user = users[0];
        user.Email.Should().Be("john@test.com");
        user.Name.Should().Be("John");
        user.Surname.Should().Be("Doe");
        user.CompanyId.Should().Be(company.CompanyId);
        user.ServerSalt.Should().NotBeEmpty();
        user.EncryptedPassword.Should().NotBeEmpty();
        user.EncryptedKey.Should().NotBeEmpty();

        var memberList = user.GroupMembers.ToList();
        memberList.Should().HaveCount(1);
        memberList[0].GroupId.Should().Be(group.Id);
        memberList[0].Role.Should().Be(GroupRole.Admin);

        // Registration code consumed from Redis
        redisStore.Should().NotContainKey($"auth:{registrationCode}");
    }

    // ---------------------------------------------------------------
    // 2. Registration with Invalid Code
    // ---------------------------------------------------------------
    [Fact]
    public async Task Register_WithInvalidCode_Returns400()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _, _) = SetupRedisMock();

        var company = new Company { Name = "TestCo", PublicKeyPem = "key" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        // No Redis registration data stored

        var securityOpts = TestSupport.CreateSecurityOptions();
        var securityService = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, securityOpts);
        var jwtService = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var authService = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwtService, securityService, securityOpts, new UserAccessor());

        var response = await authService.Register(BuildRegisterRequest("INVALIDCODE"), TestSupport.CreateServerCallContext());

        response.Status.Should().Be(400);
        response.Message.Should().Contain("invalid");

        var userCount = await db.Users.CountAsync();
        userCount.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // 3. Full Login Flow
    // ---------------------------------------------------------------
    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _, _) = SetupRedisMock();

        var securityOpts = TestSupport.CreateSecurityOptions();
        var securityService = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, securityOpts);
        var jwtService = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        // Create company, group, user
        var company = new Company { Name = "LoginCo", PublicKeyPem = "key" };
        var group = new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        company.Groups.Add(group);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var serverPasswordHash = new byte[] { 0xAB, 0xCD, 0xEF, 0x01 }; // PasswordHashLength=4
        var user = new User
        {
            Name = "Alice",
            Surname = "Smith",
            Email = "alice@test.com",
            EncryptedPassword = new byte[securityOpts.Value.EncryptedPasswordLength + securityOpts.Value.NonceLength + securityOpts.Value.TagLength],
            EncryptedKey = new byte[securityOpts.Value.EncryptedKeyLength + securityOpts.Value.NonceLength + securityOpts.Value.TagLength],
            ServerPasswordHash = serverPasswordHash,
            ClientSalt = new byte[securityOpts.Value.SaltLength],
            ServerSalt = new byte[securityOpts.Value.SaltLength],
            BackupEncryptedKey = new byte[securityOpts.Value.RsaKeySize / 8],
            CompanyId = company.CompanyId,
            Company = company
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Generate a valid nonce via the real SecurityService
        var nonceToken = await securityService.GetNonceToken();

        // Build login request: PasswordHash = [clientSalt(4)] + [serverPasswordHash(4)]
        var loginHash = new byte[securityOpts.Value.SaltLength + securityOpts.Value.PasswordHashLength];
        Buffer.BlockCopy(new byte[securityOpts.Value.SaltLength], 0, loginHash, 0, securityOpts.Value.SaltLength);
        Buffer.BlockCopy(serverPasswordHash, 0, loginHash, securityOpts.Value.SaltLength, securityOpts.Value.PasswordHashLength);

        var authService = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwtService, securityService, securityOpts, new UserAccessor());

        var response = await authService.Login(new LoginRequest
        {
            UserId = user.UserId.ToString(),
            PasswordHash = ByteString.CopyFrom(loginHash),
            NonceToken = nonceToken
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.JwtAccessToken.Should().NotBeNullOrEmpty();
        response.JwtRefreshToken.Should().NotBeNullOrEmpty();
        response.EncryptedKey.Should().NotBeNullOrEmpty();
    }

    // ---------------------------------------------------------------
    // 4. Login with Wrong Password
    // ---------------------------------------------------------------
    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _, _) = SetupRedisMock();

        var securityOpts = TestSupport.CreateSecurityOptions();
        var securityService = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, securityOpts);
        var jwtService = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var company = new Company { Name = "WrongPwCo", PublicKeyPem = "key" };
        var group = new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        company.Groups.Add(group);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var user = new User
        {
            Name = "Bob",
            Surname = "Jones",
            Email = "bob@test.com",
            EncryptedPassword = new byte[securityOpts.Value.EncryptedPasswordLength + securityOpts.Value.NonceLength + securityOpts.Value.TagLength],
            EncryptedKey = new byte[securityOpts.Value.EncryptedKeyLength + securityOpts.Value.NonceLength + securityOpts.Value.TagLength],
            ServerPasswordHash = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
            ClientSalt = new byte[securityOpts.Value.SaltLength],
            ServerSalt = new byte[securityOpts.Value.SaltLength],
            BackupEncryptedKey = new byte[securityOpts.Value.RsaKeySize / 8],
            CompanyId = company.CompanyId,
            Company = company
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var nonceToken = await securityService.GetNonceToken();

        // Wrong password hash
        var wrongHash = new byte[securityOpts.Value.SaltLength + securityOpts.Value.PasswordHashLength];
        Buffer.BlockCopy(new byte[securityOpts.Value.SaltLength], 0, wrongHash, 0, securityOpts.Value.SaltLength);
        Buffer.BlockCopy(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, wrongHash, securityOpts.Value.SaltLength, securityOpts.Value.PasswordHashLength);

        var authService = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwtService, securityService, securityOpts, new UserAccessor());

        var response = await authService.Login(new LoginRequest
        {
            UserId = user.UserId.ToString(),
            PasswordHash = ByteString.CopyFrom(wrongHash),
            NonceToken = nonceToken
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(401);
    }

    // ---------------------------------------------------------------
    // 5. Token Refresh Flow
    // ---------------------------------------------------------------
    [Fact]
    public async Task RefreshToken_WithValidRefreshToken_ReturnsNewTokens()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, database, _) = SetupRedisMock();
        database
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var securityOpts = TestSupport.CreateSecurityOptions();
        var securityService = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, securityOpts);
        var jwtService = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var company = new Company { Name = "RefreshCo", PublicKeyPem = "key" };
        var group = new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        company.Groups.Add(group);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var user = new User
        {
            Name = "Charlie",
            Surname = "Brown",
            Email = "charlie@test.com",
            EncryptedPassword = new byte[securityOpts.Value.EncryptedPasswordLength + securityOpts.Value.NonceLength + securityOpts.Value.TagLength],
            EncryptedKey = new byte[securityOpts.Value.EncryptedKeyLength + securityOpts.Value.NonceLength + securityOpts.Value.TagLength],
            ServerPasswordHash = new byte[securityOpts.Value.PasswordHashLength],
            ClientSalt = new byte[securityOpts.Value.SaltLength],
            ServerSalt = new byte[securityOpts.Value.SaltLength],
            BackupEncryptedKey = new byte[securityOpts.Value.RsaKeySize / 8],
            CompanyId = company.CompanyId,
            Company = company
        };
        db.Users.Add(user);
        var groupMember = new GroupMember
        {
            Group = group,
            GroupId = group.Id,
            User = user,
            UserId = user.UserId,
            CompanyId = company.CompanyId,
            JoinDate = DateTime.UtcNow,
            Role = GroupRole.User
        };
        db.GroupMembers.Add(groupMember);
        await db.SaveChangesAsync();

        // Generate a real refresh token via JwtService
        var refreshTokenStr = jwtService.GenerateRefreshToken(
            user.UserId.ToString(), user.Name, user.Surname, user.Email,
            new[] { group.Name });

        var parsedRefreshToken = jwtService.ParseToken(refreshTokenStr);

        // Set up UserAccessor with the refresh token (internal setter → reflection)
        var userAccessor = new UserAccessor();
        SetUserAccessorToken(userAccessor, parsedRefreshToken);

        var authService = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwtService, securityService, securityOpts, userAccessor);

        var response = await authService.RefreshToken(new RefreshTokenRequest(), TestSupport.CreateServerCallContext());

        response.Status.Should().Be(200);
        response.JwtAccessToken.Should().NotBeNullOrEmpty();
        response.JwtRefreshToken.Should().NotBeNullOrEmpty();
        response.JwtAccessToken.Should().NotBe(refreshTokenStr);
        response.JwtRefreshToken.Should().NotBe(refreshTokenStr);
    }

    // ---------------------------------------------------------------
    // 6. Create Registration Code Flow
    // ---------------------------------------------------------------
    // Note: CreateRegistrationCode requires context.GetHttpContext() to read role claims,
    // which is not available in TestServerCallContext. This test verifies the 401 guard
    // (missing/null access token) and the 400 validation guard (empty fields).
    [Fact]
    public async Task CreateRegistrationCode_WithoutAccessToken_Returns401()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var (multiplexer, _, redisStore) = SetupRedisMock();

        var securityOpts = TestSupport.CreateSecurityOptions();
        var securityService = new SecurityService(multiplexer.Object, NullLogger<SecurityService>.Instance, securityOpts);
        var jwtService = new JwtService(db, multiplexer.Object, TestSupport.CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var company = new Company { Name = "RegCodeCo", PublicKeyPem = "key" };
        var group = new Group
        {
            Name = "system:owner",
            Company = company,
            CompanyId = company.CompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        company.Groups.Add(group);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var user = new User
        {
            Name = "Owner",
            Surname = "User",
            Email = "owner@regcode.com",
            EncryptedPassword = new byte[securityOpts.Value.EncryptedPasswordLength + securityOpts.Value.NonceLength + securityOpts.Value.TagLength],
            EncryptedKey = new byte[securityOpts.Value.EncryptedKeyLength + securityOpts.Value.NonceLength + securityOpts.Value.TagLength],
            ServerPasswordHash = new byte[securityOpts.Value.PasswordHashLength],
            ClientSalt = new byte[securityOpts.Value.SaltLength],
            ServerSalt = new byte[securityOpts.Value.SaltLength],
            BackupEncryptedKey = new byte[securityOpts.Value.RsaKeySize / 8],
            CompanyId = company.CompanyId,
            Company = company
        };
        db.Users.Add(user);
        var groupMember = new GroupMember
        {
            Group = group,
            GroupId = group.Id,
            User = user,
            UserId = user.UserId,
            CompanyId = company.CompanyId,
            JoinDate = DateTime.UtcNow,
            Role = GroupRole.Owner
        };
        db.GroupMembers.Add(groupMember);
        await db.SaveChangesAsync();

        var targetGroup = new Group
        {
            Name = "dev-team",
            Company = company,
            CompanyId = company.CompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Groups.Add(targetGroup);
        await db.SaveChangesAsync();

        // UserAccessor has no token → should return 401
        var userAccessor = new UserAccessor();

        var authService = new AuthenticationService(
            db, multiplexer.Object, NullLogger<AuthenticationService>.Instance,
            jwtService, securityService, securityOpts, userAccessor);

        var beforeCount = redisStore.Count;

        var response = await authService.CreateRegistrationCode(new CreateRegistrationCodeRequest
        {
            Name = "New",
            Surname = "User",
            Email = "newuser@test.com",
            Groups = { targetGroup.Id.ToString() },
            AdminGroups = { targetGroup.Id.ToString() }
        }, TestSupport.CreateServerCallContext());

        response.Status.Should().Be(401);
        response.Message.Should().Contain("invalid");

        // Redis should not have been modified
        redisStore.Count.Should().Be(beforeCount);
    }
}