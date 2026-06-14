using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Server.Storage.Data;
using Server.Storage.Interfaces;
using Server.Storage.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

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

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<StorageGrpcService>();
app.MapControllers();

app.Run();

public partial class Program { }
