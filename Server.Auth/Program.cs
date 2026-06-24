using Serilog;
using Server.Auth.Interfaces;
using Server.Auth.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Server.Auth.Middlewares;
using Server.Auth.Options;
using AspNetCoreRateLimit;
using Common.Helpers;

EnvConfigurationHelper.LoadEnvFile();

// Метод расширения из пакета ConfigurationPlaceholders
var serilogConfig = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();
EnvConfigurationHelper.ResolvePlaceholders(serilogConfig);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(serilogConfig)
    .CreateLogger();

// Создание и настройка приложения
var builder = WebApplication.CreateBuilder(args);

// Подключение Serilog
builder.Host.UseSerilog();

// Замена .env переменных на значения переменных окружения
EnvConfigurationHelper.ResolvePlaceholders(builder.Configuration);

// ── Настройка сервисов ───────────────────────────────────────────────────────

// Ограничение частоты запросов
builder.Services.AddOptions();
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddHttpContextAccessor();

// Подключение к Redis
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection") ?? throw new InvalidOperationException("Redis connection string not found.");
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));

// Подключение к БД
var connectionString = builder.Configuration.GetConnectionString("PostgresConnection") ?? throw new InvalidOperationException("Postgres connection string not found.");
Console.WriteLine(connectionString);
builder.Services.AddDbContext<DataGuardDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// Options Services
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection("Jwt")).ValidateDataAnnotations();
builder.Services.AddOptions<CompanyManagerOptions>().Bind(builder.Configuration.GetSection("CompanyManager")).ValidateDataAnnotations();
builder.Services.AddOptions<SecurityOptions>().Bind(builder.Configuration.GetSection("Security")).ValidateDataAnnotations();

// JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// Security service
builder.Services.AddSingleton<ISecurityService, SecurityService>();

// User accessor
builder.Services.AddScoped<UserAccessor>();

// REST контроллеры
builder.Services.AddControllers();

// gRPC сервисы
builder.Services.AddGrpc();

// ── Пайплайн приложения ─────────────────────────────────────────────────────

var app = builder.Build();

// OpenAPI endpoint (только для разработки)
if (app.Environment.IsDevelopment())
{
}

app.UseHttpsRedirection();

// Ограничение частоты запросов
app.UseIpRateLimiting();

// JWT middleware
app.UseMiddleware<JwtMiddleware>();

// gRPC endpoints
app.MapGrpcService<AuthenticationService>();
app.MapGrpcService<CompanyManagerService>();
app.MapGrpcService<SecurityRequestsService>();

// REST endpoints
app.MapControllers();

app.Run();

public partial class Program { }