using Client.Engine;
using Client.Engine.Interfaces;
using Client.Engine.Interceptors;
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
using Serilog;
using System.IO;
using System.Threading.Channels;

// ─── Конфигурация Serilog ───────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Запуск DataGuard Engine...");

    var builder = WebApplication.CreateBuilder(args);

    // Подключаем Serilog к хосту
    builder.Host.UseSerilog();

    // Sqlite
    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataGuard", "Agent", "Agent.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    var connectionString = $"Data Source={dbPath}";
    builder.Services.AddDbContext<AgentDbContext>(options => options.UseSqlite(connectionString));

    // Options
    builder.Services.AddOptions<SecurityOptions>().Bind(builder.Configuration.GetSection("Security")).ValidateDataAnnotations();
    builder.Services.AddOptions<ConnectionOptions>().Bind(builder.Configuration.GetSection("Connection"));

    // Providers
    builder.Services.AddSingleton<IUserAgentProvider, UserAgentProvider>();
    builder.Services.AddSingleton<IJwtTokenProvider, JwtTokenProvider>();
    builder.Services.AddSingleton<IKeyProvider, KeyProvider>();

    // Перехватчик аутентификации локальных клиентов
    builder.Services.AddSingleton<ClientAuthInterceptor>();

    // ─── Настройка транспорта: Named Pipe (Windows) / Unix Socket (Linux, macOS) ───
    builder.WebHost.ConfigureKestrel(options =>
    {
#if WINDOWS
        options.ListenNamedPipe("DataGuardPipe", listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
#else
        var socketPath = Path.Combine(Path.GetTempPath(), ".dataguard-engine.sock");
        if (File.Exists(socketPath)) File.Delete(socketPath);
        options.ListenUnixSocket(socketPath, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
        // Установка прав: только владелец может читать/писать (аналог chmod 600).
        // В .NET нет прямого API для прав unix-сокета,
        // но файл создаётся с учётом umask процесса.
#endif
    });

    builder.Services.AddGrpc(options =>
    {
        options.Interceptors.Add<ClientAuthInterceptor>();
    });

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

    // Регистрация сервисов в DI (для консоли)
    builder.Services.AddScoped<AuthenticationService>();
    builder.Services.AddScoped<CompanyManagerService>();
    builder.Services.AddScoped<StorageClientService>();

    // Консольные команды
    if (Environment.UserInteractive)
        builder.Services.AddHostedService<ConsoleCommandWorker>();

    var app = builder.Build();

    // Убедимся, что база данных SQLite создана
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
        db.Database.EnsureCreated();
    }

    // gRPC сервисы
    app.MapGrpcService<AuthenticationService>();
    app.MapGrpcService<CompanyManagerService>();
    app.MapGrpcService<StorageClientService>();

    Log.Information("DataGuard Engine запущен успешно");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Критическая ошибка при запуске DataGuard Engine");
}
finally
{
    Log.CloseAndFlush();
}