using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProtoBuf.Grpc.Server;
using System.Text;
using GrpcContracts;
using Server.Modules;
using Server.Services;
using Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов в контейнер зависимостей

// Регистрация MemoryCache для кэширования nonce и других данных
builder.Services.AddMemoryCache();

// Регистрация DbContext с PostgreSQL
builder.Services.AddDbContext<DataGuardDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddGrpc();

// Регистрация сервисов с областью видимости Scoped
builder.Services.AddScoped<IAccountServise, AccountService>();
builder.Services.AddScoped<IJwtModule, JwtModule>();

// Регистрация middleware
builder.Services.AddScoped<TimeSynchronization>();
builder.Services.AddScoped<IdempotencyMiddleware>();

// JWT Authentication
var jwtConfig = builder.Configuration.GetSection("Jwt");
var secretKey = jwtConfig["SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey не настроен");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtConfig["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtConfig["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Добавление middleware для синхронизации времени
app.UseMiddleware<TimeSynchronization>();

// Добавление middleware для идемпотентности запросов
app.UseMiddleware<IdempotencyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<AccountService>();

app.Run();
