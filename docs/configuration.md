# Конфигурация

Настройка приложения, переменные окружения и классы Options.

## Переменные окружения

Локальный запуск использует `appsettings.Development.json`. Для продакшн используйте переменные окружения.

### Сервер

| Переменная                     | Описание                                      | Требуется |
|-------------------------------|-----------------------------------------------|-----------|
| `ConnectionStrings__DefaultConnection` | Строка подключения к PostgreSQL       | Да        |
| `Jwt__Key`                     | Ключ подписи JWT (минимум 32 символа)        | Да        |
| `Jwt__Issuer`                  | Эмитент JWT                                   | Да        |
| `Jwt__Audience`                | Адресат JWT                                   | Да        |
| `Redis__Configuration`         | Строка подключения к Redis                   | Да        |

### Client.Engine

| Переменная                     | Описание                                      | Требуется |
|-------------------------------|-----------------------------------------------|-----------|
| `Redis__Configuration`         | Строка подключения к Redis                   | Да        |
| `Queue__Name`                  | Имя очереди для обработки                        | Да        |
| `ApiEndpoint`                  | Эндпоинт API для аутентификации сервиса      | Да        |
| `Jwt__ClientKey`                | JWT ключ для клиента                           | Да        |

## Классы Options

### JwtOptions ([Server/Options/JwtOptions.cs](Server/Options/JwtOptions.cs))

```json
{
  "Jwt": {
    "Key": "development-key-change-in-production",
    "Issuer": "DataGuard",
    "Audience": "DataGuardClients"
  },
  "ApiSettings": {
    "ClientAuthKey": "client-secret-key"
  },
  "QueueSettings": {
    "Name": "default-queue"
  }
}  "Jwt": {
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
  "ApiSettings": {
    "ClientAuthKey": "client-secret-key"
  },
  "QueueSettings": {
    "Name": "default-queue"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Привязка конфигурации

В [Server/Program.cs](Server/Program.cs):

```csharp
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<CompanyManagerOptions>(builder.Configuration.GetSection("CompanyManager"));
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.Configure<QueueSettings>(builder.Configuration.GetSection("QueueSettings"));
```

## Настройки для разных окружений

- `appsettings.json` — базовые настройки
- `appsettings.Development.json` — переопределения для разработки (игнорируется в git)
- `appsettings.Production.json` — переопределения для продакшн

## Управление секретами

Для продакшн используйте:
- [User Secrets](https://learn.microsoft.com/ru-ru/aspnet/core/security/app-secrets) для разработки
- [Azure Key Vault](https://learn.microsoft.com/ru-ru/azure/key-vault/) или аналоги для продакшн
- Docker secrets при запуске в контейнерах

## Переопределения для Docker Compose

Переменные окружения можно передать через файл `.env`:

```env
# PostgreSQL
POSTGRES_PASSWORD=secure-password

# Redis
REDIS_PASSWORD=redis-password

# JWT
JWT_KEY=production-key-here
JWT_ISSUER=DataGuard
JWT_AUDIENCE=DataGuardClients

# Очередь
QUEUE_NAME=production-queue

# API
CLIENT_AUTH_KEY=production-client-key