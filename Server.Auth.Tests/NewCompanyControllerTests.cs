using Common.Server.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Controllers;
using Server.Auth.Services;
using StackExchange.Redis;

namespace Server.Auth.Tests;

public class NewCompanyControllerTests
{
    [Fact]
    public async Task CreateCompanyAsync_HttpRequest_ReturnsForbidden()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection;
        var controller = await CreateControllerAsync(db, "master");
        controller.ControllerContext = CreateControllerContext(isHttps: false);

        var result = await controller.CreateCompanyAsync(new NewCompanyRequest("Acme", "owner@example.com", "master"));

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        db.Companies.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "owner@example.com", "master", "название")]
    [InlineData("Acme", "bad-email", "master", "email")]
    [InlineData("Acme", "owner@example.com", "", "мастер")]
    public async Task CreateCompanyAsync_InvalidRequest_ReturnsBadRequest(string name, string email, string masterKey, string expectedMessagePart)
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection;
        var controller = await CreateControllerAsync(db, "master");

        var result = await controller.CreateCompanyAsync(new NewCompanyRequest(name, email, masterKey));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NewCompanyResponse>(badRequest.Value);
        response.Message.Should().Contain(expectedMessagePart);
        db.Companies.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCompanyAsync_InvalidMasterKey_ReturnsGenericErrorAndDoesNotCreateCompany()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection;
        var controller = await CreateControllerAsync(db, "expected-master");

        var result = await controller.CreateCompanyAsync(new NewCompanyRequest("Acme", "owner@example.com", "wrong-master"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NewCompanyResponse>(badRequest.Value);
        response.Message.Should().Be("Не удалось создать компанию.");
        response.RegistrationCode.Should().BeNull();
        db.Companies.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCompanyAsync_DuplicateCompanyName_ReturnsGenericConflict()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection;
        await db.Companies.AddAsync(new Company { Name = "Acme" });
        await db.SaveChangesAsync();
        var controller = await CreateControllerAsync(db, "master");

        var result = await controller.CreateCompanyAsync(new NewCompanyRequest("Acme", "owner@example.com", "master"));

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var response = Assert.IsType<NewCompanyResponse>(conflict.Value);
        response.Message.Should().Be("Не удалось создать компанию.");
        response.RegistrationCode.Should().BeNull();
    }

    [Fact]
    public async Task CreateCompanyAsync_ValidRequest_CreatesCompanyAndOwnerRegistrationCode()
    {
        using var db = TestSupport.CreateDbContext(out var connection);
        await using var _ = connection;
        var redisStore = new Dictionary<string, string>();
        var redis = TestSupport.CreateRedisMock();
        redis.Database
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When>((key, value, _, _) => redisStore[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        redis.Database
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, value, _, _, _) => redisStore[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        redis.Database
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>((key, value, _, _, _) => redisStore[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        var controller = await CreateControllerAsync(db, "master", redis.Multiplexer.Object);

        var result = await controller.CreateCompanyAsync(new NewCompanyRequest("Acme", "owner@example.com", "master"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NewCompanyResponse>(ok.Value);
        response.Status.Should().Be(200);
        response.RegistrationCode.Should().NotBeNullOrWhiteSpace().And.HaveLength(12);
        var company = await db.Companies.Include(c => c.Groups).SingleAsync();
        company.Name.Should().Be("Acme");
        company.Groups.Should().ContainSingle(g => g.Name == "system:owner");
        redisStore.Should().ContainSingle();
        redisStore.Keys.Single().Should().Be($"auth:{response.RegistrationCode}");
        redisStore.Values.Single().Should().Contain("owner@example.com");
    }

    private static async Task<NewCompanyController> CreateControllerAsync(
        DataGuardDbContext db,
        string validMasterKey,
        IConnectionMultiplexer? redis = null)
    {
        var redisMock = redis is null ? TestSupport.CreateRedisMock().Multiplexer.Object : redis;
        var securityOptions = TestSupport.CreateSecurityOptions();
        var securityService = new SecurityService(redisMock, NullLogger<SecurityService>.Instance, securityOptions);
        byte[] masterHash = await securityService.HashPasswordAsync(validMasterKey, securityOptions.Value.MasterKeySalt);
        var controller = new NewCompanyController(
            NullLogger<NewCompanyController>.Instance,
            securityService,
            securityOptions,
            TestSupport.CreateCompanyManagerOptions(masterHash),
            db,
            redisMock);
        controller.ControllerContext = CreateControllerContext(isHttps: true);
        return controller;
    }

    private static ControllerContext CreateControllerContext(bool isHttps)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = isHttps ? "https" : "http";
        return new ControllerContext { HttpContext = httpContext };
    }
}