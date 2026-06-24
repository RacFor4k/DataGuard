using System.Reflection;
using System.Security.Claims;
using Grpc.Core;
using Grpc.Core.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Server.Auth.Options;
using Server.Auth.Services;
using StackExchange.Redis;

namespace Server.Auth.Tests;

internal static class TestSupport
{
    public static ServerCallContext CreateServerCallContext() => TestServerCallContext.Create(
        "test",
        "localhost",
        DateTime.UtcNow.AddMinutes(1),
        new Metadata(),
        CancellationToken.None,
        "127.0.0.1",
        null,
        null,
        _ => Task.CompletedTask,
        () => new WriteOptions(),
        _ => { });

    public static ServerCallContext CreateServerCallContextWithClaims(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Use reflection to create HttpContextServerCallContext (internal class in Grpc.AspNetCore.Server)
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Grpc.AspNetCore.Server")
            ?? Assembly.Load("Grpc.AspNetCore.Server");

        var type = assembly.GetType("Grpc.AspNetCore.Server.Internal.HttpContextServerCallContext");
        if (type == null)
            throw new InvalidOperationException("Cannot find Grpc.AspNetCore.Server.Internal.HttpContextServerCallContext type");

        var ctors = type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
        var ctor = ctors.FirstOrDefault(c => c.GetParameters().Length > 0
            && c.GetParameters()[0].ParameterType == typeof(HttpContext));
        if (ctor == null)
            throw new InvalidOperationException("Cannot find HttpContextServerCallContext constructor with HttpContext parameter");

        var parameters = ctor.GetParameters();
        var args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            args[i] = ResolveParameter(parameters[i].ParameterType, parameters[i].Name, httpContext);
        }

        return (ServerCallContext)ctor.Invoke(args)!;
    }

    private static object ResolveParameter(Type type, string? name, HttpContext httpContext)
    {
        if (type == typeof(HttpContext)) return httpContext;
        if (type == typeof(string)) return name switch
        {
            "method" => "/test/TestMethod",
            "host" => "localhost",
            "peer" => "127.0.0.1",
            _ => string.Empty
        };
        if (type == typeof(DateTime)) return DateTime.UtcNow.AddMinutes(1);
        if (type == typeof(Metadata)) return new Metadata();
        if (type == typeof(CancellationToken)) return CancellationToken.None;
        if (type == typeof(AuthContext)) return new AuthContext(null, new Dictionary<string, List<AuthProperty>>());
        if (type == typeof(ContextPropagationToken)) return null!;
        if (type == typeof(Func<Task>)) return (Func<Task>)(() => Task.CompletedTask);
        if (type == typeof(Func<WriteOptions, WriteOptions>)) return (Func<WriteOptions, WriteOptions>)(w => w);
        if (type == typeof(Action<Metadata>)) return (Action<Metadata>)(_ => { });
        if (type == typeof(bool)) return false;
        return type.IsValueType ? Activator.CreateInstance(type)! : null!;
    }

    public static DataGuardDbContext CreateDbContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DataGuardDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new DataGuardDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    public static IOptions<SecurityOptions> CreateSecurityOptions() => Microsoft.Extensions.Options.Options.Create(new SecurityOptions
    {
        NonceSecretKey = [1, 2, 3, 4, 5, 6, 7, 8],
        MasterKeySalt = [9, 8, 7, 6],
        SaltLength = 4,
        PasswordHashLength = 4,
        EncryptedPasswordLength = 8,
        EncryptedKeyLength = 8,
        NonceLength = 2,
        TagLength = 2,
        RsaKeySize = 64,
        Argon2 = new SecurityOptions.Argon2Options
        {
            DegreeOfParallelism = 1,
            Iterations = 1,
            MemorySize = 1024
        }
    });

    public static IOptions<CompanyManagerOptions> CreateCompanyManagerOptions(byte[] masterKeyHash) =>
        Microsoft.Extensions.Options.Options.Create(new CompanyManagerOptions { MasterKeyHash = masterKeyHash });

    public static IOptions<JwtOptions> CreateJwtOptions() => Microsoft.Extensions.Options.Options.Create(new JwtOptions
    {
        Issuer = "DataGuard.Tests",
        Audience = "DataGuard.Tests.Client",
        Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray(),
        AccessTokenExpiration = TimeSpan.FromMinutes(15),
        RefreshTokenExpiration = TimeSpan.FromHours(1)
    });

    public static (Mock<IConnectionMultiplexer> Multiplexer, Mock<IDatabase> Database) CreateRedisMock()
    {
        var database = new Mock<IDatabase>(MockBehavior.Loose);
        var multiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Loose);
        multiplexer
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        return (multiplexer, database);
    }
}