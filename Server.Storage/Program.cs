using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Serilog;
using Server.Storage.Data;
using Server.Storage.Interfaces;
using Server.Storage.Services;
using StackExchange.Redis;
using HealthChecks.UI.Client;

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Запуск сервиса Server.Storage");

    var builder = WebApplication.CreateBuilder(args);

    // Замена стандартного логирования на Serilog
    builder.Host.UseSerilog();

    builder.Services.AddControllers();

    // Аутентификация JWT с настраиваемым ClockSkew
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Parse(builder.Configuration["Jwt:ClockSkew"] ?? "00:01:00")
            };
        });

    builder.Services.AddAuthorization();

    // Ограничение частоты запросов по IP
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
    builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    builder.Services.AddInMemoryRateLimiting();

    var connectionString = builder.Configuration.GetConnectionString("PostgresConnection") ?? throw new InvalidOperationException("Postgres connection string not found.");
    builder.Services.AddDbContext<StorageDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

    var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection") ?? throw new InvalidOperationException("Redis connection string not found.");
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));

    builder.Services.AddMinio(configureClient => configureClient
        .WithEndpoint(builder.Configuration["Minio:Endpoint"]!)
        .WithCredentials(builder.Configuration["Minio:AccessKey"]!, builder.Configuration["Minio:SecretKey"]!)
        .WithSSL(false)
        .Build());

    builder.Services.AddGrpc();

    builder.Services.AddScoped<IStorageFileRepository, StorageFileRepository>();
    builder.Services.AddScoped<IStorageDirectoryRepository, StorageDirectoryRepository>();
    builder.Services.AddSingleton<IStoragePathValidator, StoragePathValidator>();
    builder.Services.AddSingleton<IStorageNonceService, StorageNonceService>();
    builder.Services.AddSingleton<IStorageBlobStore, MinioBlobStore>();
    builder.Services.AddScoped<IStorageMetadataService, StorageMetadataService>();
    builder.Services.AddScoped<IStorageLinkService, StorageLinkService>();
    builder.Services.AddScoped<IOwnerIdentityProvider, JwtOwnerIdentityProvider>();

    // Проверки работоспособности
    var redisConnStr = builder.Configuration.GetConnectionString("RedisConnection")!;
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgresql", tags: ["db", "postgres"])
        .AddRedis(redisConnStr, name: "redis", tags: ["cache", "redis"])
        .AddCheck("minio", () =>
        {
            // Минимальная проверка: конфигурация MinIO загружена
            var endpoint = builder.Configuration["Minio:Endpoint"];
            return string.IsNullOrEmpty(endpoint)
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("MinIO endpoint не настроен")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
        }, tags: ["storage", "minio"]);

    var app = builder.Build();

    app.UseHttpsRedirection();

    // Ограничение частоты запросов
    app.UseIpRateLimiting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGrpcService<StorageGrpcService>();
    app.MapControllers();

    // Endpoint проверки работоспособности
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Критическая ошибка при запуске сервиса Server.Storage");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
