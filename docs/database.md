# База данных

## PostgreSQL

Основное хранилище данных. Подключение настраивается через `DefaultConnection` в `appsettings.json`.

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=dataguard;Username=postgres;Password=postgres"
}
```

## Entity Framework Core

- `DataGuardDbContext` зарегистрирован в DI как scoped.
- Используется соглашение об именовании в snake_case через `UseSnakeCaseNamingConvention()`.
- Миграции хранятся в `Server/Migrations/`.

## Redis

Используется для кэширования и распределённого состояния. Настраивается через `Redis:Configuration`.

```json
"Redis": {
  "Configuration": "localhost:6379"
}
```

## Паттерн доступа к данным

- Сервисы получают доступ к данным через репозитории или напрямую через `DataGuardDbContext`.
- EF Core управляет пулом подключений и оптимизацией запросов.

## Миграции

Применяются командой:
```bash
dotnet ef database update --project Server --startup-project Server
```

### Создание новой миграции
```bash
dotnet ef migrations add <MigrationName> --project Server --output-dir Migrations