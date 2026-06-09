# Архитектура

Общий дизайн, границы компонентов и потоки выполнения.

## Обзор

DataGuard — многокомпонентное приложение на .NET 10:

- `Server` — хост ASP.NET Core с REST-контроллерами и gRPC-сервисами.
- `Contracts` — общие контракты, включая определения Protobuf.
- `Common` — общие вспомогательные классы и утилиты.
- `Client.Engine` — фоновый рабочий процесс/потребитель.

Инфраструктурные зависимости:

- PostgreSQL через Entity Framework Core.
- Redis через StackExchange.Redis.

## Запуск сервера

На основе [Server/Program.cs](Server/Program.cs):

- Регистрирует `IConnectionMultiplexer` для Redis.
- Регистрирует `DataGuardDbContext` с Npgsql и именованием в snake_case.
- Привязывает `JwtOptions` и `CompanyManagerOptions`.
- Регистрирует `IJwtService`, `ISecurityService`, `UserAccessor`.
- Добавляет REST-контроллеры и gRPC.
- Маппит gRPC-сервисы:
  - `AuthenticationService`
  - `SecurityRequestsService`
- Применяет `JwtMiddleware` перед обработкой эндпоинтов.

## Слои коммуникации

- REST — стандартные эндпоинты ASP.NET Core контроллеров.
- gRPC — сервисы на основе Protocol Buffers; ожидается совместимость с Base64 для REST-эквивалентов (по правилам репозитория).

## Безопасность

Подробности модели угроз и мер по смягчению см. в файле [security.md](security.md).