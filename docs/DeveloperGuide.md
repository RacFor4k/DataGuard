# Руководство разработчика

## 1. Системные требования

| Компонент | Версия | Примечание |
|:---|:---|:---|
| .NET SDK | 10.0 | Требуется для сборки всех проектов |
| Docker + Docker Compose | — | Для запуска PostgreSQL 18, Redis, MinIO |
| Git | — | Для клонирования репозитория |
| IDE | Visual Studio 2022 / VS Code / Rider | Поддержка C# 13 и .NET 10 |

### Сборка решения

```bash
# Клонирование (ветка experimental)
git clone --branch experimental https://github.com/RacFor4k/DataGuard.git

# Сборка всех проектов
dotnet build DataGuard.slnx

# Запуск всех тестов
dotnet test
```

---

## 2. Развертывание инфраструктуры

### 2.1. Переменные окружения

Создайте файл `.env` в корне проекта (не коммитьте в репозиторий):

```bash
# PostgreSQL
DB_USER=dataguard
DB_PASSWORD=<надёжный_пароль>
DB_NAME=dataguard

# Redis
REDIS_PASSWORD=<надёжный_пароль>

# MinIO
MINIO_ROOT_USER=minioadmin
MINIO_ROOT_PASSWORD=<надёжный_пароль>

# JWT
JWT_KEY=<base64_ключ_минимум_32_байта>
JWT_SECRET=<строковый_секрет_для_Server.Storage>

# Security
SECURITY_NONCE_SECRET_KEY=<base64_ключ_минимум_32_байта>
SECURITY_MASTER_KEY_SALT=<base64_соль_32_байта>

# Company Manager
COMPANY_MANAGER_MASTER_KEY_HASH=<base64_хеш_64_байта>
```

### 2.2. Запуск Docker-контейнеров

```bash
docker-compose up -d
```

Запускаются три сервиса:

| Сервис | Порт | Описание |
|:---|:---|:---|
| PostgreSQL 18 | 5432 | Базы `dataguard` (Server.Auth) и `dataguard_storage` (Server.Storage) |
| Redis | 6379 | Nonce-токены, чёрный список JWT, данные регистрации |
| MinIO | 9000 (API), 9001 (консоль) | S3-совместимое blob-хранилище |

### 2.3. Применение миграций

```bash
# Server.Auth
dotnet ef database update \
  --project Server.Auth/Server.Auth.csproj \
  --startup-project Server.Auth/Server.Auth.csproj

# Server.Storage
dotnet ef database update \
  --project Server.Storage/Server.Storage.csproj \
  --startup-project Server.Storage/Server.Storage.csproj
```

---

## 3. Запуск серверов

### 3.1. Server.Auth

```bash
dotnet run --project Server.Auth/Server.Auth.csproj
```

Приложение доступно на `https://localhost:7203` (gRPC).

### 3.2. Server.Storage

```bash
dotnet run --project Server.Storage/Server.Storage.csproj
```

Приложение доступно на `https://localhost:8081` (gRPC + REST).

### 3.3. Client.Engine

```bash
dotnet run --project Client.Engine/Client.Engine.csproj
```

gRPC-сервер доступен через Named Pipe `DataGuardPipe`. SQLite создаётся автоматически в `%LOCALAPPDATA%/DataGuard/Agent/Agent.db`.

---

## 4. Создание новой миграции

При изменении доменных моделей (сущностей в `Common/Server/Models/` или `Server.Storage/Models/`):

```bash
# 1. Создание миграции
dotnet ef migrations add <ОписательноеИмя> \
  --project <Проект>.csproj \
  --startup-project <Проект>.csproj

# 2. Проверка сгенерированного SQL
dotnet ef migrations script \
  --project <Проект>.csproj \
  --startup-project <Проект>.csproj

# 3. Применение
dotnet ef database update \
  --project <Проект>.csproj \
  --startup-project <Проект>.csproj
```

---

## 5. Структура решения

Подробное описание структуры и связей между модулями — в [Architecture.md](./Architecture.md).

```
DataGuard.slnx
├── Server.Auth/         # gRPC-сервис аутентификации (9 RPC)
├── Server.Storage/       # gRPC + REST сервис хранилища (21 RPC + 2 REST)
├── Client.Engine/        # Клиентский движок (прокси + криптография)
├── DataGuard.UI/         # Avalonia 11 GUI-приложение (прототип)
├── Contracts/            # Protobuf-контракты (6 proto-файлов)
├── Common/               # Общие модели (User, Company, Group) и JwtHelper
├── Server.Auth.Tests/    # Тесты Server.Auth
├── Server.Storage.Tests/ # Тесты Server.Storage
├── Client.Engine.Tests/  # Тесты Client.Engine
└── Common.Tests/         # Тесты Common
```

---

## 6. Тестирование

### Подход

- **Unit-тесты:** проверка изолированной логики сервисов (валидация, криптография, парсинг)
- **Интеграционные тесты:** проверка взаимодействия с PostgreSQL и Redis через `WebApplicationFactory`
- **Фреймворк:** xUnit (выводится из шаблонов ASP.NET Core)
- **Моки:** Moq или аналоги

### Запуск тестов

```bash
# Все тесты
dotnet test

# Конкретный проект
dotnet test Server.Auth.Tests/Server.Auth.Tests.csproj
dotnet test Server.Storage.Tests/Server.Storage.Tests.csproj
dotnet test Client.Engine.Tests/Client.Engine.Tests.csproj
```

---

## 7. Добавление нового gRPC-метода

### 7.1. Определение контракта

1. Добавьте RPC и сообщения в соответствующий `.proto` файл в `Contracts/Protos/`:

```protobuf
// Contracts/Protos/auth.proto
rpc NewMethod (NewMethodRequest) returns (NewMethodResponse);

message NewMethodRequest {
    string field = 1;
}

message NewMethodResponse {
    int32 status = 1;
    string message = 2;
}
```

2. Если метод предназначен для вызова от GUI через Client.Engine, создайте также упрощённый контракт в `Contracts/Protos/Client/`.

### 7.2. Реализация на сервере

1. Реализуйте метод в соответствующем сервисе (например, `Server.Auth/Services/AuthenticationService.cs`):

```csharp
public override async Task<NewMethodResponse> NewMethod(
    NewMethodRequest request,
    ServerCallContext context)
{
    // Логика
    return new NewMethodResponse { Status = 200, Message = "OK" };
}
```

### 7.3. Реализация в Client.Engine (если требуется)

1. Добавьте метод в интерфейс `IStorageService` (или создайте новый)
2. Реализуйте прокси-метод в `StorageClientService` (или новом сервисе)
3. Зарегистрируйте сервис в `Client.Engine/Program.cs`

### 7.4. Сборка и проверка

```bash
dotnet build DataGuard.slnx
dotnet test
```

---

## 8. Конфигурация

### 8.1. Паттерн Options

Все настройки привязаны к классам через `IOptions<T>`:

| Проект | Класс | Секция в appsettings.json |
|:---|:---|:---|
| Server.Auth | `JwtOptions` | `Jwt` |
| Server.Auth | `SecurityOptions` | `Security` |
| Server.Auth | `CompanyManagerOptions` | `CompanyManager` |
| Client.Engine | `SecurityOptions` | `Security` |

### 8.2. Подстановка переменных окружения

В `appsettings.json` используются шаблоны `${ENV_VAR}`. ASP.NET Core автоматически подставляет значения из переменных окружения.

---

## 9. Кодстайл и соглашения

### Соглашения по именованию

| Элемент | Стиль | Пример |
|:---|:---|:---|
| Публичные классы, интерфейсы, методы, свойства | PascalCase | `AuthenticationService`, `IJwtService`, `GenerateTokenAsync` |
| Локальные переменные, параметры | camelCase | `userId`, `accessToken`, `dbContext` |
| Приватные поля | `_camelCase` (с подчёркиванием) | `_logger`, `_dbContext`, `_jwtOptions` |
| Интерфейсы | Префикс `I` | `IJwtService`, `ISecurityService` |
| Асинхронные методы | Суффикс `Async` | `VerifyTokenAsync`, `HashPasswordAsync` |

### Комментарии

- Язык комментариев — **русский**
- XML-документация (`/// <summary>`) — обязательна для всех `public` методов интерфейсов
- Внутренние комментарии — только для неочевидной логики (алгоритмы, криптография)
- Запрещены избыточные комментарии (`// инкремент счётчика`)

### Безопасность

- Пароли и ключи — **никогда** не появляются в логах, ответах или исключениях
- Все криптографические сравнения — через `CryptographicOperations.FixedTimeEquals`
- Временные буферы с чувствительными данными — обнуляются через `CryptographicOperations.ZeroMemory`
- Секреты — только через переменные окружения или `appsettings.json` (не хардкодить)
- Ошибки аутентификации — GENERIC-сообщения без утечки информации

Полный список правил безопасности — в [SECURITY.md](../SECURITY.md) и [AGENTS.md](../AGENTS.md).

---

## 10. Решение часто возникающих проблем

### Ошибка: «Redis connection string not found»

Убедитесь, что переменная окружения `REDIS_PASSWORD` задана и `docker-compose up -d` выполнен.

### Ошибка: «Postgres connection string not found»

Убедитесь, что переменные `DB_USER`, `DB_PASSWORD` заданы, PostgreSQL запущен, и миграции применены.

### Ошибка при сборке: «The type or namespace name 'Protos' could not be found»

Выполните `dotnet restore` перед сборкой. Protobuf-файлы компилируются при восстановлении пакетов.

### Named Pipe не работает (Client.Engine)

Убедитесь, что Client.Engine запущен до DataGuard.UI. Named Pipe `DataGuardPipe` создаётся при старте процесса.