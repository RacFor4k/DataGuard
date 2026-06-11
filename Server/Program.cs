using Server.Interfaces;
using Server.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Server.Middlewares;
using Server.Options;

// Создание и настройка приложения
var builder = WebApplication.CreateBuilder(args);

// ── Настройка сервисов ───────────────────────────────────────────────────────

// Подключение к Redis
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection") ?? throw new InvalidOperationException("Redis connection string not found.");
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));

// Подключение к БД
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Postgres connection string not found.");
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

// JWT middleware
app.UseMiddleware<JwtMiddleware>();

// gRPC endpoints
app.MapGrpcService<AuthenticationService>();
app.MapGrpcService<CompanyManagerService>();
app.MapGrpcService<SecurityRequestsService>();

// REST endpoints
app.MapControllers();

app.Run();
