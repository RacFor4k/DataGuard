# Repository Guidelines

## Project Structure & Module Organization
DataGuard is a .NET 10 solution stored in `DataGuard.slnx` (custom XML). Server-side code is split between `Server.Auth` for authentication APIs, gRPC services, EF Core migrations, JWT/rate-limit logic, and `Server.Storage` for storage APIs, MinIO integration, EF Core data access, and migrations. `Contracts/Protos` owns shared gRPC contracts; client-facing proto variants live under `Contracts/Protos/Client`. `Client.Engine` contains client services, workers, interceptors, and gRPC client logic. `Client.Manager`, `Client.AuthLib`, and `Client.Styles` compose the Avalonia desktop UI stack. `Common` contains shared models, helpers, and UI view-model primitives. Solution test projects currently include `Common.Tests`, `Client.Engine.Tests`, and `Server.Storage.Tests`; `Server.Auth.Tests` exists but is not listed in `DataGuard.slnx`.

## Build, Test, and Development Commands
- `dotnet build DataGuard.slnx` — build all solution projects after completing the user's task, not after every internal subtask. If build reports errors, fix them before finishing. If build reports warnings, including warnings in unchanged areas, fix them and report what changed.
- `dotnet test` — run tests only after completing a global user task; small local tasks do not require a test run.
- `dotnet test Common.Tests/Common.Tests.csproj` — run one test project; replace path for another test project.
- `docker-compose up -d` — start PostgreSQL 18 on `5432`, Redis on `6379`, and MinIO on `9000`/`9001`.
- `dotnet run --project Server.Auth/Server.Auth.csproj` — run the auth server.
- `dotnet run --project Server.Storage/Server.Storage.csproj` — run the storage server.
- `dotnet ef migrations add <Name> --project Server.Auth/Server.Auth.csproj --startup-project Server.Auth/Server.Auth.csproj` — add auth DB migration.

## Coding Style & Naming Conventions
All projects target `net10.0`; nullable reference types and implicit usings are enabled across projects. Package versions are centrally managed in `Directory.Packages.props`; update versions there, not in individual `.csproj` files. Public C# symbols use PascalCase, locals and parameters use camelCase, interfaces use the `I` prefix, and async APIs use the `Async` suffix. `Client.Manager` enables Avalonia compiled bindings by default. Write clean, efficient code only. If a workaround is needed, ask the user first and offer several solution options.

## Testing Guidelines
Tests use xUnit with `Microsoft.NET.Test.Sdk`; projects also use FluentAssertions, Moq, coverlet, and ASP.NET test host packages where needed. Test method names follow behavior-style names such as `Login_InvalidNonceToken_Returns400`. When behavior spans multiple solution projects, create or update integration tests that verify those projects interact correctly with each other.

## Commit & Pull Request Guidelines
Recent history mostly uses Conventional Commits: `feat(scope): ...`, `fix: ...`, `refactor(scope): ...`, `docs: ...`, `chore: ...`. Russian and English subjects both appear; keep the prefix consistent and scope changes narrowly. No PR template is present under `.github`.

## Documentation & Security Instructions
Documentation rules live in `docs/DOCS.AGENTS.md`: project documentation is Russian, code fences must specify language, API docs must cover request/response parameters, headers, status codes, logic, and input constraints. Update related docs in the same change when APIs or architecture change. Use `@SECURITY.md` when writing code sensitive to attacker-controlled inputs, security boundaries, authentication, secrets, cryptography, or critical bug risk. Avoid loading it into context when it is not relevant.
