using Grpc.Core;
using Grpc.Core.Testing;
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

    public static DataGuardDbContext CreateDbContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DataGuardDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new DataGuardDbContext(options, NullLogger<DataGuardDbContext>.Instance);
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