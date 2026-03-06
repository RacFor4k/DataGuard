# DataGuard — Проект

## Обзор проекта

**DataGuard** — это приложение для безопасного управления данными с архитектурой клиент-сервер. Проект включает в себя:

- **Backend (Server)**: ASP.NET Core Web API на .NET 10.0 с поддержкой gRPC и PostgreSQL
- **Frontend (FrontEnd)**: Веб-интерфейс на React (судя по node_modules)
- **WebDemo**: Демонстрационный веб-проект

## Технологии

### Backend (Server)
- **Фреймворк**: ASP.NET Core 10.0
- **ORM**: Entity Framework Core 10.0.2
- **База данных**: PostgreSQL (Npgsql)
- **gRPC**: protobuf-net.Grpc.AspNetCore
- **Кеширование**: IMemoryCache
- **Контейнеризация**: Docker

### Frontend (FrontEnd)
- **Библиотеки**: React, React-DOM, React-Router
- **Стилизация**: Sass
- **Валидация**: Zod
- **Сборка**: Vite
- **Linting**: ESLint

## Структура проекта

```
DataGuard/
├── Server/                 # Backend API
│   ├── Controllers/        # MVC контроллеры
│   ├── Models/
│   │   ├── Db/           # Модели базы данных
│   │   │   └── Identity/ # User, Group, Company, UserGroup
│   │   └── gRPC/         # gRPC контракты
│   ├── Contracts/        # Сервисные контракты (AccountContract, FileSystemContract)
│   ├── Services/         # Бизнес-логика
│   │   └── DataGuardDbContext.cs
│   ├── Modules/          # Модули (CacheModule)
│   └── Middleware/       # Промежуточное ПО
├── FrontEnd/             # Frontend приложение
│   └── node_modules/     # Зависимости npm
├── WebDemo/              # Демонстрационный проект
└── DataGuard.slnx        # Решение Visual Studio
```

## Сборка и запуск

### Backend (Server)

```bash
# Восстановление зависимостей
dotnet restore Server/Server.csproj

# Сборка
dotnet build Server/Server.csproj -c Release

# Запуск (Development)
dotnet run --project Server/Server.csproj

# Запуск в Docker
docker build -t dataguard-server ./Server
docker run -p 8080:8080 -p 8081:8081 dataguard-server
```

### Frontend (FrontEnd)

```bash
cd FrontEnd

# Установка зависимостей
npm install

# Запуск dev-сервера
npm run dev

# Сборка
npm run build
```

### Конфигурация базы данных

В `Server/appsettings.json` указана строка подключения к PostgreSQL:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=MyDb;Username=username;Password=password;"
  }
}
```

## Модули и функциональность

### Модуль кеширования (CacheModule)
- Реализует интерфейс `CacheModuleI` для работы с памятью
- Использует `IMemoryCache` для хранения временных данных
- Включает реализацию `AuthNonceCahce` для хранения nonce авторизации

### Контракты аккаунта (AccountContract)
gRPC сервис для управления аккаунтами:
- `SignUp` — регистрация пользователя
- `SignIn` — вход в систему
- `CreateCompany` — создание компании
- `LiquidateCompany` — ликвидация компании

### Модели базы данных

#### User (identity)
- Id, Name, Surname, Email
- PublicKey, EncyptedToken
- CreationTime, Confirmed

#### Company
- Id, Name, EmployeesCount
- AdditionalModules (флаги)
- Storage параметры
- Status (Active/Closed/Archived/Initial)

#### Group
- Id, Name
- SecurityPermissions (флаги)
- CompanyId

#### UserGroup (связующая таблица)
- UserId, GroupId
- Role (Guest/User/Admin)
- JoinedAt

## Практики разработки

### Код
- Включены `Nullable` и `ImplicitUsings`
- Использование gRPC с protobuf-сериализацией
- Entity Framework с Fluent API конфигурацией

### Игнорирование файлов
Проект использует расширенный `.gitignore` для Visual Studio и .NET:
- Игнорируются `bin/`, `obj/`, `.vs/`
- Игнорируются пользовательские настройки и секреты (`appsettings.*.json`, `.env`)
- Игнорируются `node_modules/`

## Примечания

- Проект находится в разработке (некоторые файлы могут быть незавершёнными)
- FrontEnd директория содержит только `node_modules` — исходный код фронтенда отсутствует или не инициализирован
- WebDemo проект требует дополнительной настройки
