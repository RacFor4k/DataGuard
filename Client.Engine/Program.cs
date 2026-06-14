using Client.Engine;
using Client.Engine.Interfaces;
using Client.Engine.Models;
using Client.Engine.Options;
using Client.Engine.Services;
using Client.Engine.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

// Sqlite
var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataGuard", "Agent", "Agent.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            var connectionString = $"Data Source={dbPath}";
            builder.Services.AddDbContext<AgentDbContext>(options => options.UseSqlite(connectionString));

// Options
builder.Services.AddOptions<SecurityOptions>().Bind(builder.Configuration.GetSection("Security")).ValidateDataAnnotations();

// Providers
builder.Services.AddSingleton<IUserAgentProvider, UserAgentProvider>();
builder.Services.AddSingleton<IJwtTokenProvider, JwtTokenProvider>();
builder.Services.AddSingleton<IKeyProvider, KeyProvider>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenNamedPipe("DataGuardPipe", listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();

builder.Services.AddHttpClient();

// gRPC клиенты
builder.Services.AddGrpcClient<Contracts.Protos.Auth.Authentication.AuthenticationClient>(options =>
{
    options.Address = new Uri(builder.Configuration["Grpc:AuthUrl"] ?? throw new InvalidOperationException("Missing Grpc:AuthUrl configuration"));
});
builder.Services.AddGrpcClient<Contracts.Protos.CompanyManager.CompanyManager.CompanyManagerClient>((serviceProvider, options) =>
{
    options.Address = new Uri(builder.Configuration["Grpc:CompanyManagerUrl"] ?? throw new InvalidOperationException("Missing Grpc:CompanyManagerUrl configuration"));
});
builder.Services.AddGrpcClient<Contracts.Protos.Security.SecurityService.SecurityServiceClient>((serviceProvider, options) =>
{
    options.Address = new Uri(builder.Configuration["Grpc:SecurityUrl"] ?? throw new InvalidOperationException("Missing Grpc:SecurityUrl configuration"));
});
builder.Services.AddGrpcClient<Contracts.Protos.Storage.StorageService.StorageServiceClient>((serviceProvider, options) =>
{
    options.Address = new Uri(builder.Configuration["Grpc:StorageUrl"] ?? throw new InvalidOperationException("Missing Grpc:StorageUrl configuration"));
});

// .AddCallCredentials(async (context, metadata, serviceProvider) =>
// {
//     var tokenProvider = serviceProvider.GetRequiredService<IJwtTokenProvider>();
//
//     try
//     {
//         var token = await tokenProvider.GetOrRefreshTokenAsync();
//         metadata.Add("Authorization", $"Bearer {token}");
//     }
//     catch (Exception ex)
//     {
//         var logger = serviceProvider.GetRequiredService<ILogger<JwtTokenProvider>>();
//         logger.LogError(ex, "Failed to inject call credentials.");
//     }
// });

// Регистрация сервисов в DI (для консоли)
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<CompanyManagerService>();
builder.Services.AddScoped<StorageClientService>();

// Консольные команды
if (Environment.UserInteractive)
    builder.Services.AddHostedService<ConsoleCommandWorker>();

var app = builder.Build();

            // Ensure SQLite database is created
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                db.Database.EnsureCreated();
            }

// gRPC сервисы
app.MapGrpcService<AuthenticationService>();
app.MapGrpcService<CompanyManagerService>();
app.MapGrpcService<StorageClientService>();app.Run();
