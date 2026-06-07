using Server.Interfaces;
using Server.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Server.Middlewares;

// Создание и настройка приложения
var builder = WebApplication.CreateBuilder(args);

// ── Настройка сервисов ───────────────────────────────────────────────────────

// Подключение к Redis
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection") ?? throw new InvalidOperationException("Redis connection string not found.");
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));

// Подключение к БД
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Postgres connection string not found.");
builder.Services.AddDbContext<DataGuardDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// Security service
builder.Services.AddScoped<ISecurityService, SecurityService>();

// User accessor
builder.Services.AddScoped<UserAccessor>();

// REST контроллеры
builder.Services.AddControllers();

// OpenAPI (Swagger) для разработки
builder.Services.AddOpenApi();

// gRPC сервисы
builder.Services.AddGrpc();

// ── Пайплайн приложения ─────────────────────────────────────────────────────

var app = builder.Build();

// OpenAPI endpoint (только для разработки)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// JWT middleware
app.UseMiddleware<JwtMiddleware>();

// gRPC endpoints
app.MapGrpcService<AuthenticationService>();
app.MapGrpcService<SecurityRequestsService>();

// REST endpoints
app.MapControllers();

app.Run();
