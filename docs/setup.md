# Setup

Локальная разработка, зависимости, запуск.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Docker Compose](https://docs.docker.com/compose/)

## Infrastructure

Запуск зависимостей (PostgreSQL, Redis):

```bash
docker-compose up -d
```

Сервисы из `docker-compose.yml` должны подняться на:
- PostgreSQL — `localhost:5432`
- Redis — `localhost:6379`

## Server (REST + gRPC)

```bash
cd Server
dotnet run
```

По умолчанию:
- REST + Swagger: `https://localhost:<port>` и `/swagger`
- gRPC: `http://localhost:8081` (можно указать явно через `--urls`)

### Swagger UI

После запуска откройте `/swagger` в браузере для тестирования REST endpoints.

### gRPC endpoint

gRPC сервис слушает порт `8081` по протоколу HTTP/2. Убедитесь, что `docker-compose` не конфликтует с этим портом.

## Client.Engine

Фоновый воркер, обрабатывающий очередь сообщений.

```bash
cd Client.Engine
dotnet run
```

Переменные окружения и конфигурация берутся из `appsettings.json` / `appsettings.Development.json`.

## Database Migrations

Миграции Entity Framework Core находятся в [Server/Migrations](Server/Migrations).

### Применение миграций

```bash
cd Server
dotnet ef database update
```

### Создание новой миграции

```bash
dotnet ef migrations add <MigrationName>
```

### Сброс базы

```bash
dotnet ef database drop
dotnet ef database update
```

## Environment Variables

Минимальный набор для локального запуска в [Server/appsettings.Development.json](Server/appsettings.Development.json):
- `ConnectionStrings:DefaultConnection` — строка подключения к PostgreSQL
- `Jwt:Key` — секретный ключ для подписи JWT
- `Jwt:Issuer`, `Jwt:Audience` — издатель и аудитория токена
- `Redis:Configuration` — строка подключения к Redis

## Troubleshooting

### Порт 8081 занят

Измените порт gRPC в `JwtOptions` или в аргументах запуска:

```bash
dotnet run --urls "http://localhost:8082"
```

### PostgreSQL не отвечает

Проверьте, что контейнер поднялся:

```bash
docker-compose ps
```

Просмотрите логи:

```bash
docker-compose logs postgres
```

### Redis connection refused

Убедитесь, что контейнер запущен и порт `6379` открыт:

```bash
docker-compose logs redis