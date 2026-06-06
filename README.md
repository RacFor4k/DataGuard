# DataGuard

Система аутентификации на базе .NET, предоставляющая REST API и gRPC сервисы для управления пользователями.

## Технологии

- **.NET 8.0** (Web API)
- **gRPC** для межсервисного взаимодействия
- **Swagger/OpenAPI** для документации API
- **Protobuf** для сериализации сообщений
- **In-Memory** репозиторий для разработки

## ОПИСАНИЕ

DataGuard — это система аутентификации с поддержкой двух протоколов:
- **REST API** — HTTP endpoints для регистрации и входа пользователей
- **gRPC** — высокопроизводительный протокол для межсервисного взаимодействия

Ключевой особенностью является совместимость форматов запросов и ответов между REST и gRPC. Ответы возвращаются в формате Base64-encoded JSON, что упрощает интеграцию между системами.

## СТРУКТУРА ПРОЕКТА

```
Server/
├── Controllers/          # REST API контроллеры
│   └── AuthController.cs # Контроллер аутентификации
├── Middlewares/          # Промежуточное ПО
│   └── Base64DecodingMiddleware.cs # Декодирование Base64-encoded запросов
├── Models/               # DTO модели
│   ├── RegisterModel.cs # Модель регистрации
│   └── LoginModel.cs    # Модель входа
├── Protos/               # gRPC определения
│   └── auth.proto       # Сервис аутентификации gRPC
├── Services/             # Сервисы-реализации
│   └── InMemoryUserRepository.cs # In-memory репозиторий
├── Interfaces/           # Интерфейсы репозитория
│   └── IUserRepository.cs # Абстракция репозитория
├── Helpers/              # Вспомогательные методы
│   └── Base64Helper.cs  # Утилиты Base64
├── Tests/                # Тесты
│   ├── InMemoryUserRepositoryTests.cs
│   ├── AuthControllerIntegrationTests.cs
│   └── Base64HelperTests.cs
└── appsettings.json      # Конфигурация приложения
```

## ИМЕТАПЫ

### REST API

**Регистрация:** `POST /api/auth/register`

```json
{
  "email": "user@example.com",
  "password": "securePassword123"
}
```

**Вход:** `POST /api/auth/login`

```json
{
  "email": "user@example.com",
  "password": "securePassword123"
}
```

### gRPC

**Регистрация:** `Authentication/Register`
- Request: `RegisterRequest { email, password }`
- Response: `RegisterResponse { success, message }`

**Вход:** `Authentication/Login`
- Request: `LoginRequest { email, password }`
- Response: `LoginResponse { success, token, message }`

## ЗАПУСК ПРИЛОЖЕНИЯ

```bash
# Запуск в режиме разработки (включает Swagger)
dotnet run --project Server.csproj

# Для доступа к Swagger UI
http://localhost:5000/swagger
```

## BASE64-КОДИРОВАНИЕ ЗАПРОСОВ

Для сотрудничающих систем поддерживается отправка запросов в формате Base64-encoded JSON:

```json
{
  "payload": "eyJlbWFpbCI6InVzZXJAbm9uZS5jb20iLCJwYXNzd29yZCI6Im9wZW4ifQ=="
}
```

Важно: Middleware автоматически декодирует такие запросы перед обработкой контроллерами.

## НАСТРОЙКА

Текущая реализация использует in-memory репозиторий, что подходит для разработки и тестирования. В продакшене рекомендуется заменить:
1. `InMemoryUserRepository` на реализацию с базой данных (Entity Framework, SQL Server, PostgreSQL, и т.д.)
2. Генерацию токена на использование JWT с реальным алгоритмом подписи
3. Хранение паролей с реальным хешированием

## ТЕСТИРОВАНИЕ

```bash
# Запуск всех тестов
dotnet test

# Запуск тестов из конкретного проекта
dotnet test Server.Tests/Server.Tests.csproj
```

## ТРЕБОВАНИЯ

- .NET 8.0 SDK или новее
- Visual Studio 2022, VS Code или Visual Studio for Mac
- .NET CLI инструмент

## ЛИЦЕНЗИЯ

Проект предоставлен как демонстрационный пример архитектуры аутентификации.