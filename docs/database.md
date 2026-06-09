# Database

## PostgreSQL

Primary data store. Connection configured via `DefaultConnection` in `appsettings.json`.

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=dataguard;Username=postgres;Password=postgres"
}
```

## Entity Framework Core

- `DataGuardDbContext` registered in DI as scoped.
- SnakeCase naming convention enforced via `UseSnakeCaseNamingConvention()`.
- Migrations stored in `Server/Migrations/`.

## Redis

Used for caching and distributed state. Configured via `RedisConnection`.

```json
"ConnectionStrings": {
  "RedisConnection": "localhost:6379"
}
```

## Data Access Pattern

- Services access data through repositories or directly via `DataGuardDbContext`.
- EF Core handles connection pooling and query optimization.

## Migrations

Apply with:
```bash
dotnet ef database update --project Server --startup-project Server