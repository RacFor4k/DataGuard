# DataGuard — Проект

## Обзор проекта

**DataGuard** — это система безопасного управления данными с архитектурой клиент-сервер. Проект реализует backend на ASP.NET Core с поддержкой gRPC для межсервисного взаимодействия и JWT-аутентификации, а также десктопный клиент на Avalonia UI.

## Архитектура

```
DataGuard/
├── Server/                 # Backend API (ASP.NET Core 10.0)
│   ├── Controllers/        # MVC контроллеры
│   ├── Middleware/         # Промежуточное ПО
│   │   ├── Idempotency.cs        # Идемпотентность запросов
│   │   └── TimeSynchronization.cs # Синхронизация времени
│   ├── Models/
│   │   └── Db/
│   │       └── Identity/   # Модели БД: User, Company, Group, UserGroup
│   ├── Modules/            # Модули
│   │   ├── CacheModule.cs  # Кэширование (IMemoryCache)
│   │   └── JwtModule.cs    # JWT токены
│   ├── Services/           # Бизнес-логика
│   │   ├── AccountService.cs
│   │   └── DataGuardDbContext.cs
│   ├── appsettings.json    # Конфигурация
│   └── Program.cs          # Точка входа
├── Client/                 # Десктопный клиент (Avalonia UI)
│   ├── ViewModels/         # ViewModel (MVVM)
│   ├── Views/              # Представления
│   └── Models/             # Модели
├── GrpcContracts/          # gRPC контракты (protobuf-net)
│   ├── Account/            # Контракты аутентификации
│   │   ├── AuthNonce.cs
│   │   ├── SignIn.cs
│   │   ├── SignUp.cs
│   │   └── RefreshToken.cs
│   ├── Company/            # Контракты компаний
│   │   ├── CreateCompany.cs
│   │   └── LiquidateCompany.cs
│   └── AccountContract.cs  # Интерфейс сервиса
├── GrpcContracts.Tests/    # Тесты контрактов
│   └── ContractIntegrityTests.cs
└── WebDemo/                # Демонстрационный веб-проект
```

## Технологии

### Backend (Server)
- **Фреймворк**: ASP.NET Core 10.0 (.NET 10)
- **ORM**: Entity Framework Core 10.0.2
- **База данных**: PostgreSQL (Npgsql)
- **gRPC**: protobuf-net.Grpc.AspNetCore 1.2.2
- **Аутентификация**: JWT (Microsoft.AspNetCore.Authentication.JwtBearer)
- **Кеширование**: IMemoryCache
- **Контейнеризация**: Docker

### Client (Desktop)
- **Фреймворк**: Avalonia UI 11.3.12
- **MVVM**: CommunityToolkit.Mvvm 8.2.1
- **Платформа**: .NET 10

### gRPC Контракты (GrpcContracts)
- **Сериализация**: protobuf-net 3.2.46
- **gRPC**: protobuf-net.Grpc 1.2.2

### Тесты (GrpcContracts.Tests)
- **Фреймворк**: xUnit 2.9.3
- **Assertions**: FluentAssertions 7.2.0
- **Code Coverage**: coverlet.collector 6.0.4

## Сборка и запуск

### Backend (Server)

```bash
# Восстановление зависимостей
dotnet restore Server/Server.csproj

# Сборка
dotnet build Server/Server.csproj -c Release

# Запуск (Development)
dotnet run --project Server/Server.csproj

# Запуск с профилем https
dotnet run --project Server/Server.csproj --launch-profile https
```

**URL по умолчанию:**
- HTTP: `http://localhost:5167`
- HTTPS: `https://localhost:7182`

### Client (Desktop)

```bash
# Восстановление зависимостей
dotnet restore Client/Client.csproj

# Сборка
dotnet build Client/Client.csproj -c Release

# Запуск
dotnet run --project Client/Client.csproj
```

### Docker

```bash
# Сборка образа
docker build -t dataguard-server ./Server

# Запуск контейнера
docker run -p 8080:8080 -p 8081:8081 dataguard-server
```

### Тесты

```bash
# Запуск тестов
dotnet test GrpcContracts.Tests/GrpcContracts.Tests.csproj
```

## Конфигурация

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=MyDb;Username=username;Password=password;"
  },
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "DataGuard",
    "Audience": "DataGuard",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

**Важно:** Файлы `appsettings.*.json` игнорируются в git (кроме базового `appsettings.json`). Используйте `appsettings.Development.json` для локальной разработки.

## gRPC Контракты

### IAccountServise

Интерфейс сервиса аутентификации:

| Метод | Описание |
|-------|----------|
| `AuthNonce` | Получение nonce для аутентификации |
| `SignUp` | Регистрация пользователя (TODO) |
| `SignIn` | Вход в систему (с ECDSA подписью) |
| `RefreshToken` | Обновление токена доступа |

### Модель аутентификации

1. **AuthNonce**: Клиент получает одноразовый nonce
2. **SignIn**: Клиент подписывает nonce своим приватным ключом (ECDSA SHA256)
3. **Сервер**: Проверяет подпись с использованием публичного ключа из БД
4. **Токены**: При успехе выдаются AccessToken (JWT) и RefreshToken

## Модели базы данных

### User (identity)
```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string Email { get; set; }
    public string PublicKey { get; set; }      // ECDSA публичный ключ
    public string EncyptedToken { get; set; }
    public DateTime CreationTime { get; set; } // DEFAULT: CURRENT_TIMESTAMP
    public string? RefreshToken { get; set; }
}
```

### Company
```csharp
public class Company
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int EmployeesCount { get; set; }
    public AdditionalModules AdditionalModules { get; set; } // Flags enum
    public CompanyStatus Status { get; set; } // Active/Closed/Archived/Initial
    // ... параметры хранилища
}
```

### Group
```csharp
public class Group
{
    public int Id { get; set; }
    public string Name { get; set; }
    public SecurityPermissions SecurityPermissions { get; set; } // Flags enum
    public int CompanyId { get; set; }
}
```

### UserGroup (связующая таблица)
```csharp
public class UserGroup
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public Role Role { get; set; } // Guest/User/Admin
    public DateTime JoinedAt { get; set; } // DEFAULT: CURRENT_TIMESTAMP
}
```

## Middleware

### IdempotencyMiddleware
- Обрабатывает заголовок `Idempotency-Key`
- Кэширует ответы для POST/PUT/PATCH/DELETE запросов
- Требует заголовок `User-uuid` для изоляции кэша
- Время жизни кэша: 24 часа (абсолютное), 60 минут (скользящее)

### TimeSynchronization
- Проверяет заголовок `X-Request-Time`
- Отклоняет запросы, если время клиента отличается более чем на 1 час

## Практики разработки

### Код
- Включены `Nullable` и `ImplicitUsings`
- gRPC с protobuf-сериализацией
- EF Core с Fluent API конфигурацией в `OnModelCreating`

### Тестирование
- xUnit для модульных тестов
- FluentAssertions для читаемых проверок
- Тесты целостности контрактов (ProtoMember номера, сериализация)

### Игнорирование файлов
- `bin/`, `obj/`, `.vs/` — артефакты сборки
- `node_modules/` — зависимости npm
- `appsettings.*.json` — конфигурация (кроме базового)
- `.env` — переменные окружения

## Примечания

- **FrontEnd**: Директория содержит только `node_modules` — исходный код требует развертывания
- **WebDemo**: Требует дополнительной настройки
- **Миграции БД**: Используйте `dotnet ef migrations add` и `dotnet ef database update` для управления схемой PostgreSQL
