extern alias ServerAuth;

using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Common.Server.Models;
using Contracts.Protos.Auth;
using Contracts.Protos.CompanyManager;
using Contracts.Protos.Storage;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ServerAuth::Server.Auth.Interfaces;
using ServerAuth::Server.Auth.Options;
using ServerAuth::Server.Auth.Services;
using Server.Storage.Data;
using Server.Storage.Interfaces;
using Server.Storage.Services;
using StackExchange.Redis;

namespace Server.Storage.Tests;

public sealed class AuthStorageEndToEndTests
{
    [Fact]
    public async Task FullCompanyOwnerLoginStoragePipeline_Succeeds()
    {
        using var authDb = CreateAuthDbContext(out var authConnection);
        await using var authConnectionScope = authConnection.ConfigureAwait(false);
        var redisStore = new Dictionary<string, string>();
        var (redis, _) = CreateRedisMock(redisStore);

        var security = new Mock<ISecurityService>();
        security.Setup(s => s.VerifyNonceToken(It.IsAny<string>())).ReturnsAsync(true);
        security.Setup(s => s.GenerateSalt()).Returns([5, 6, 7, 8]);

        var jwtService = new JwtService(authDb, redis.Object, CreateJwtOptions(), NullLogger<JwtService>.Instance);

        var companyUserAccessor = new UserAccessor();
        var companyService = new CompanyManagerService(
            NullLogger<CompanyManagerService>.Instance,
            security.Object,
            CreateSecurityOptions(),
            Options.Create(new CompanyManagerOptions { MasterKeyHash = [1, 2, 3, 4] }),
            authDb,
            redis.Object,
            companyUserAccessor);
        var authService = new AuthenticationService(
            authDb,
            redis.Object,
            NullLogger<AuthenticationService>.Instance,
            jwtService,
            security.Object,
            CreateSecurityOptions(),
            new UserAccessor());

        var createCompany = await companyService.CreateCompany(new CreateCompanyRequest
        {
            NonceToken = "company-nonce",
            MasterKey = ByteString.CopyFrom([9, 8, 7, 6, 1, 2, 3, 4]),
            CompanyName = "DataGuard E2E",
            CompanyEmail = "owner@dataguard.local"
        }, CreateServerCallContext());

        createCompany.Status.Should().Be(200);
        createCompany.RegistrationCode.Should().NotBeNullOrWhiteSpace();

        var provisionalOwnerId = Guid.NewGuid();
        SetUserJwt(companyUserAccessor, new JwtSecurityToken(claims:
        [
            new Claim(JwtRegisteredClaimNames.Typ, "access"),
            new Claim(JwtRegisteredClaimNames.Sub, provisionalOwnerId.ToString())
        ]));
        var setPublicKey = await companyService.SetCompanyPublicKey(new SetCompanyPublicKeyRequest
        {
            RegistrationCode = createCompany.RegistrationCode,
            CompanyPublicKeyPem = "public-key-pem"
        }, CreateServerCallContextWithClaims(new Claim("role", "system:owner")));

        setPublicKey.Status.Should().Be(200);

        var register = await authService.Register(new RegisterRequest
        {
            RegistrationCode = createCompany.RegistrationCode,
            EncryptedPassword = ByteString.CopyFrom([1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 3, 3]),
            EncryptedKey = ByteString.CopyFrom([4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 6, 6]),
            BackupEncryptedKey = ByteString.CopyFrom([7, 7, 7, 7, 7, 7, 7, 7]),
            PasswordHash = ByteString.CopyFrom([8, 8, 8, 8, 9, 9, 9, 9]),
            ClientSalt = ByteString.CopyFrom([8, 8, 8, 8])
        }, CreateServerCallContext());

        register.Status.Should().Be(200);
        register.Email.Should().Be("owner@dataguard.local");
        register.CompanyPublicKeyPem.Should().Be("public-key-pem");

        var login = await authService.Login(new LoginRequest
        {
            UserId = register.UserId,
            PasswordHash = ByteString.CopyFrom([8, 8, 8, 8, 9, 9, 9, 9]),
            NonceToken = "login-nonce"
        }, CreateServerCallContext());

        login.Status.Should().Be(200);
        login.JwtAccessToken.Should().NotBeNullOrWhiteSpace();
        login.JwtRefreshToken.Should().NotBeNullOrWhiteSpace();
        var verifiedAccessToken = await jwtService.VerifyTokenAsync(login.JwtAccessToken);
        verifiedAccessToken.Should().NotBeNull();
        verifiedAccessToken!.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == register.UserId);
        verifiedAccessToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == "system:owner");
        jwtService.ParseToken(login.JwtRefreshToken).Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Typ && c.Value == "refresh");
        var userId = Guid.Parse(register.UserId);
        (await authDb.Companies.CountAsync()).Should().Be(1);
        (await authDb.Users.CountAsync()).Should().Be(1);
        var ownerGroup = await authDb.Groups.SingleAsync(g => g.Name == "system:owner");
        var groupMember = await authDb.GroupMembers.SingleAsync(gm => gm.UserId == userId && gm.GroupId == ownerGroup.Id);
        groupMember.Role.Should().Be(GroupRole.Admin);

        using var storageDb = CreateStorageDbContext(out var storageConnection);
        await using var storageConnectionScope = storageConnection.ConfigureAwait(false);
        var fileRepo = new StorageFileRepository(storageDb);
        var dirRepo = new StorageDirectoryRepository(storageDb);
        var metadataService = new StorageMetadataService(storageDb);
        var storageService = new StorageGrpcService(
            fileRepo,
            dirRepo,
            new InMemoryStorageBlobStore(),
            new TrackingStorageNonceService(),
            new StoragePathValidator(),
            metadataService,
            new StorageLinkService(storageDb),
            new JwtOwnerIdentityProvider(),
            NullLogger<StorageGrpcService>.Instance);

        var unauthorizedDirectory = await storageService.NewDirectory(new NewDirectoryRequest
        {
            DirectoryPath = "/unauthorized"
        }, new TestGrpcCallContext(new DefaultHttpContext()));
        unauthorizedDirectory.Success.Should().BeFalse();

        var storageHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(verifiedAccessToken.Claims, "Jwt"))
        };
        var storageContext = new TestGrpcCallContext(storageHttpContext);
        var newDirectory = await storageService.NewDirectory(new NewDirectoryRequest
        {
            DirectoryPath = "/docs"
        }, storageContext);

        newDirectory.Success.Should().BeTrue(newDirectory.Message);
        var directoryId = Guid.Parse(newDirectory.DirectoryId);

        byte[] payload = Encoding.UTF8.GetBytes("DataGuard integration payload");
        var metadata = new FileMetadata
        {
            FileName = "report.txt",
            FilePath = "/docs",
            Size = payload.Length,
            ParentDirectoryId = directoryId.ToString()
        };
        metadata.Metadata["category"] = "pipeline";
        metadata.Metadata["owner"] = register.Email;

        var upload = await storageService.UploadFile(new TestAsyncStreamReader<UploadFileRequest>(
        [
            new UploadFileRequest { Metadata = metadata },
            new UploadFileRequest { Chunk = ByteString.CopyFrom(payload) }
        ]), storageContext);

        upload.Success.Should().BeTrue(upload.Message);
        var fileId = Guid.Parse(upload.FileId);
        var storedFile = await fileRepo.GetFileAsync(fileId, userId);
        storedFile.Should().NotBeNull();
        storedFile!.NormalizedPath.Should().Be("/docs/report.txt");
        var foreignHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())
            ], "Jwt"))
        };
        var foreignMetadata = await storageService.GetMetadata(new GetMetadataRequest { FileId = upload.FileId }, new TestGrpcCallContext(foreignHttpContext));
        foreignMetadata.Success.Should().BeFalse();

        var fileStream = new CollectingServerStreamWriter<GetFileResponse>();
        await storageService.GetFile(new GetFileRequest { FileId = upload.FileId }, fileStream, storageContext);

        fileStream.Messages.Should().NotBeEmpty();
        fileStream.Messages[0].Metadata.FileName.Should().Be("report.txt");
        fileStream.Messages.Skip(1).SelectMany(m => m.Chunk.ToByteArray()).ToArray().Should().Equal(payload);

        var updateMetadata = await storageService.UpdateMetadata(new UpdateMetadataRequest
        {
            FileId = upload.FileId,
            Metadata =
            {
                ["stage"] = "e2e",
                ["classification"] = "internal"
            }
        }, storageContext);

        updateMetadata.Success.Should().BeTrue(updateMetadata.Message);
        var getMetadata = await storageService.GetMetadata(new GetMetadataRequest { FileId = upload.FileId }, storageContext);
        getMetadata.Success.Should().BeTrue(getMetadata.Message);
        getMetadata.Metadata.Metadata.Should().ContainKey("stage").WhoseValue.Should().Be("e2e");

        var rename = await storageService.RenameFile(new RenameFileRequest
        {
            FileId = upload.FileId,
            NewName = "renamed-report.txt",
            NonceToken = "rename-1"
        }, storageContext);

        rename.Success.Should().BeTrue(rename.Message);
        var renamedFile = await fileRepo.GetFileAsync(fileId, userId);
        renamedFile.Should().NotBeNull();
        renamedFile!.FileName.Should().Be("renamed-report.txt");
        renamedFile.NormalizedPath.Should().Be("/docs/renamed-report.txt");

        var replayRename = await storageService.RenameFile(new RenameFileRequest
        {
            FileId = upload.FileId,
            NewName = "replay-report.txt",
            NonceToken = "rename-1"
        }, storageContext);

        replayRename.Success.Should().BeFalse();

        var delete = await storageService.DeleteFile(new DeleteFileRequest
        {
            FileId = upload.FileId,
            NonceToken = "delete-1"
        }, storageContext);

        delete.Success.Should().BeTrue(delete.Message);
        (await fileRepo.GetFileAsync(fileId, userId)).Should().BeNull();
        (await storageDb.Files.IgnoreQueryFilters().SingleAsync(f => f.FileId == fileId)).DeletedAtUtc.Should().NotBeNull();
    }

    private static DataGuardDbContext CreateAuthDbContext(out SqliteConnection connection)
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

    private static StorageDbContext CreateStorageDbContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new StorageDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static IOptions<SecurityOptions> CreateSecurityOptions() => Options.Create(new SecurityOptions
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

    private static IOptions<JwtOptions> CreateJwtOptions() => Options.Create(new JwtOptions
    {
        Issuer = "DataGuard.Integration.Tests",
        Audience = "DataGuard.Integration.Tests.Client",
        Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray(),
        AccessTokenExpiration = TimeSpan.FromMinutes(15),
        RefreshTokenExpiration = TimeSpan.FromHours(1),
        ClockSkew = TimeSpan.Zero
    });

    private static (Mock<IConnectionMultiplexer> Multiplexer, Mock<IDatabase> Database) CreateRedisMock(Dictionary<string, string> store)
    {
        var database = new Mock<IDatabase>(MockBehavior.Loose);
        var multiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Loose);
        multiplexer
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When>((key, value, _, _) => store[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, value, _, _, _) => store[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>((key, value, _, _, _) => store[key.ToString()] = value.ToString())
            .ReturnsAsync(true);
        database
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => store.TryGetValue(key.ToString(), out string? value) ? value : RedisValue.Null);
        database
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => store.Remove(key.ToString()))
            .ReturnsAsync(true);
        database
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => store.ContainsKey(key.ToString()));
        return (multiplexer, database);
    }

    private static ServerCallContext CreateServerCallContext() => TestServerCallContext.Create(
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

    private static ServerCallContext CreateServerCallContextWithClaims(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
        return new TestGrpcCallContext(httpContext);
    }

    private static void SetUserJwt(UserAccessor accessor, JwtSecurityToken token)
    {
        typeof(UserAccessor).GetProperty(nameof(UserAccessor.UserJwt))!.SetValue(accessor, token);
    }

    private sealed class InMemoryStorageBlobStore : IStorageBlobStore
    {
        private readonly ConcurrentDictionary<string, byte[]> _objects = new();

        public Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken ct = default)
        {
            return Task.FromResult<Stream>(new MemoryStream(_objects[$"{bucketName}/{objectName}"], writable: false));
        }

        public Task GetObjectToStreamAsync(string bucketName, string objectName, Stream targetStream, CancellationToken ct = default)
        {
            return targetStream.WriteAsync(_objects[$"{bucketName}/{objectName}"], ct).AsTask();
        }

        public async Task PutObjectAsync(string bucketName, string objectName, Stream content, long size, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            _objects[$"{bucketName}/{objectName}"] = ms.ToArray();
        }

        public Task CopyObjectAsync(string sourceBucket, string sourceObject, string destBucket, string destObject, CancellationToken ct = default)
        {
            _objects[$"{destBucket}/{destObject}"] = _objects[$"{sourceBucket}/{sourceObject}"];
            return Task.CompletedTask;
        }

        public Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken ct = default)
        {
            _objects.TryRemove($"{bucketName}/{objectName}", out _);
            return Task.CompletedTask;
        }

        public Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct = default) => Task.CompletedTask;

        public string GenerateStorageKey(string fileExtension) => $"objects/{Guid.NewGuid():N}{fileExtension}";
    }

    private sealed class TrackingStorageNonceService : IStorageNonceService
    {
        private readonly HashSet<string> _consumed = [];

        public Task<bool> TryConsumeNonceAsync(Guid ownerId, string operationName, string nonceToken, TimeSpan ttl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(nonceToken))
                return Task.FromResult(false);

            return Task.FromResult(_consumed.Add($"{ownerId:N}:{operationName}:{nonceToken}"));
        }
    }

    private sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly IEnumerator<T> _enumerator;

        public TestAsyncStreamReader(IEnumerable<T> values)
        {
            _enumerator = values.GetEnumerator();
        }

        public T Current => _enumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(_enumerator.MoveNext());
    }

    private sealed class CollectingServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public List<T> Messages { get; } = [];

        public Task WriteAsync(T message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}