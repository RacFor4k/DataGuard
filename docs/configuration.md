# Configuration

Конфигурация приложения, переменные окружения, классы Options.

## Environment Variables

Локальный запуск использует `appsettings.Development.json`. Для production используйте переменные окружения.

### Server

| Variable | Description | Required |
|----------|-------------|----------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | Yes |
| `Jwt__Key` | JWT signing key (min 32 chars) | Yes |
| `Jwt__Issuer` | JWT issuer | Yes |
| `Jwt__Audience` | JWT audience | Yes |
| `Redis__Configuration` | Redis connection string | Yes |

### Client.Engine

| Variable | Description | Required |
|----------|-------------|----------|
| `Redis__Configuration` | Redis connection string | Yes |
| `Queue__Name` | Queue name for processing | Yes |

## Options Classes

### JwtOptions ([Server/Options/JwtOptions.cs](Server/Options/JwtOptions.cs))

```json
{
  "Jwt": {
    "Key": "your-secret-key-min-32-chars",
    "Issuer": "DataGuard",
    "Audience": "DataGuardClients"
  }
}
```

### CompanyManagerOptions ([Server/Options/CompanyManagerOptions.cs](Server/Options/CompanyManagerOptions.cs))

```json
{
  "CompanyManager": {
    "Endpoint": "http://company-manager:8080"
  }
}
```

## appsettings Structure

### Server/appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dataguard;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "development-key-change-in-production",
    "Issuer": "DataGuard",
    "Audience": "DataGuardClients"
  },
  "Redis": {
    "Configuration": "localhost:6379"
  },
  "CompanyManager": {
    "Endpoint": "http://localhost:8080"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Configuration Binding

В [Server/Program.cs](Server/Program.cs):

```csharp
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<CompanyManagerOptions>(builder.Configuration.GetSection("CompanyManager"));
```

## Environment-Specific Settings

- `appsettings.json` — базовые настройки
- `appsettings.Development.json` — переопределения для разработки (git-игнорируется)
- `appsettings.Production.json` — переопределения для production

## Secrets Management

Для production используйте:
- [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) для разработки
- [Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/) или аналоги для production
- Docker secrets при запуске в контейнерах

## Docker Compose Overrides

Переменные можно передать через `.env` файл:

```env
POSTGRES_PASSWORD=secure-password
REDIS_PASSWORD=redis-password
JWT_KEY=production-key-here