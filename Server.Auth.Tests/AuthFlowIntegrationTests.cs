using System.Text.Json;
using Contracts.Protos.Auth;
using Contracts.Protos.CompanyManager;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Interfaces;
using Server.Auth.Models;
using Server.Auth.Services;
using StackExchange.Redis;

namespace Server.Auth.Tests;

public class AuthFlowIntegrationTests
{
    [Fact]
    public async Task CompanyOwnerRegistrationAndLogin_FullServiceFlow_Succeeds()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (_, database) = TestSupport.CreateRedisMock();
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When>((key, value, _, _) => redisStore[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, value, _, _, _) => redisStore[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>((key, value, _, _, _) => redisStore[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        database
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => redisStore.TryGetValue(key.ToString(), out string? value) ? value : RedisValue.Null);
        database
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => redisStore.Remove(key.ToString()))
            .ReturnsAsync(true);

        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken("nonce")).ReturnsAsync(true);
        security.Setup(s => s.GenerateSalt()).Returns([5, 6, 7, 8]);
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("access-token");
        jwt.Setup(j => j.GenerateRefreshToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("refresh-token");

        var companyService = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance,
            security.Object,
            TestSupport.CreateSecurityOptions(),
            TestSupport.CreateCompanyManagerOptions([1, 2, 3, 4]),
            db,
            database.Object);
        var authService = new AuthenticationService(
            db,
            database.Object,
            NullLogger<AuthenticationService>.Instance,
            jwt.Object,
            security.Object,
            TestSupport.CreateSecurityOptions(),
            new UserAccessor(NullLogger<UserAccessor>.Instance));

        var createCompany = await companyService.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "nonce",
            MasterKey = ByteString.CopyFrom([9, 8, 7, 6, 1, 2, 3, 4]),
            CompanyName = "DataGuard",
            CompanyEmail = "owner@dataguard.local"
        }, TestSupport.CreateServerCallContext());
        Assert.Equal(200, createCompany.Status);
        Assert.False(string.IsNullOrWhiteSpace(createCompany.RegistrationCode));

        var setPublicKey = await companyService.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = createCompany.RegistrationCode,
            CompanyPublicKeyPem = "public-key-pem"
        }, TestSupport.CreateServerCallContext());
        Assert.Equal(200, setPublicKey.Status);

        var register = await authService.Register(new RegisterRequest
        {
            RegistrationCode = createCompany.RegistrationCode,
            EncryptedPassword = ByteString.CopyFrom([1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 3, 3]),
            EncryptedKey = ByteString.CopyFrom([4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 6, 6]),
            BackupEncryptedKey = ByteString.CopyFrom([7, 7, 7, 7, 7, 7, 7, 7]),
            PasswordHash = ByteString.CopyFrom([8, 8, 8, 8, 9, 9, 9, 9]),
            ClientSalt = ByteString.CopyFrom([8, 8, 8, 8])
        }, TestSupport.CreateServerCallContext());
        Assert.Equal(200, register.Status);
        Assert.Equal("owner@dataguard.local", register.Email);
        Assert.Equal("public-key-pem", register.CompanyPublicKeyPem);

        var login = await authService.Login(new LoginRequest
        {
            UserId = register.UserId,
            PasswordHash = ByteString.CopyFrom([8, 8, 8, 8, 9, 9, 9, 9]),
            NonceToken = "nonce"
        }, TestSupport.CreateServerCallContext());

        Assert.Equal(200, login.Status);
        Assert.Equal("access-token", login.JwtAccessToken);
        Assert.Equal("refresh-token", login.JwtRefreshToken);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.Companies.CountAsync());
        Assert.Equal(1, await db.GroupMembers.CountAsync());
    }
}