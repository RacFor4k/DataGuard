# QWEN.md – Project Primer

## Project Overview
DataGuard is an authentication system built on **.NET 8.0** providing both **REST API** and **gRPC** services. It manages user registration, login, and company/role data. The architecture follows a clean separation:
- **Client.Engine** – a .NET console client that consumes the services via gRPC.
- **Server.Auth** – the authentication server exposing REST endpoints (via ASP.NET Core) and gRPC services.
- **Common / Contracts** – shared models and protobuf definitions.
- **Docker Compose** – optional PostgreSQL container for persistence (currently an in‑memory repository is used for development).

## Key Technologies
- ASP.NET Core Web API
- gRPC (protobuf)
- Entity Framework Core (SQLite for the client, PostgreSQL via Docker for the server)
- Swagger / OpenAPI for REST documentation
- JWT for token handling (interfaces defined, implementation in `JwtTokenProvider`)

## Building & Running
| Target | Command | Notes |
|--------|---------|-------|
| **All solutions** | `dotnet build` | Builds every project under the solution `DataGuard.slnx`. |
| **Run Server** | `dotnet run --project Server.Auth/Server.Auth.csproj` | Starts the authentication server on default ports (HTTP/2 for gRPC, Swagger at `/swagger`). |
| **Run Client** | `dotnet run --project Client.Engine/Client.Engine.csproj` | Launches the console client which connects to the server via named pipe `DataGuardPipe`. |
| **Tests** | `dotnet test` | Executes all unit/integration tests. Individual test projects can be targeted, e.g., `dotnet test Server.Auth.Tests/Server.Auth.Tests.csproj`. |
| **Database (Postgres) via Docker** | `docker-compose up -d` | Spins up a PostgreSQL container defined in `docker-compose.yml`. Adjust connection strings in `appsettings.json` if you switch from the in‑memory repository. |
| **Swagger UI** | After running the server, open `http://localhost:{port}/swagger` (port defined in `launchSettings.json`). |

## Development Conventions (from `AGENTS.md`)
- **Naming**: `PascalCase` for public members, `camelCase` for locals/private fields. Interfaces prefixed with `I`.
- **Async** methods end with `Async`.
- **Security**: BCrypt/PBKDF2 for password hashing, JWT for tokens, constant‑time comparisons, rate‑limiting on auth endpoints.
- **Testing**: Unit tests for business logic, integration tests covering both REST and gRPC endpoints. Mock external dependencies only when unit testing; integration tests should hit a real DB.
- **Comments**: Russian‑language comments for functions describing purpose, parameters, returns, side‑effects. TODO comments for future work are tracked in `TODO.md`.

## Important Files & Directories
- `README.md` – high‑level description.
- `Client.Engine/Program.cs` – entry point, DI setup, gRPC client configuration, SQLite DB for the client.
- `Server.Auth/Program.cs` – server bootstrap, service registration, gRPC server configuration.
- `Contracts/Protos/` – protobuf definitions (`auth.proto`, `company_manager.proto`, etc.).
- `Server.Auth/Models/` – EF Core entity models (`User.cs`, `Company.cs`, ...).
- `Server.Auth/Options/` – strongly‑typed configuration classes (e.g., `JwtOptions`).
- `docs/` – markdown docs for each component.
- `docker-compose.yml` – PostgreSQL container definition.
- `QWEN.md` – **this file** – central guidance for future agents.

## Building from Source
```bash
# Restore packages
dotnet restore
# Build all projects
dotnet build
# Run tests to verify integrity
dotnet test
```

## Running Locally (Development)
1. Start PostgreSQL (optional): `docker-compose up -d`.
2. Launch the server: `dotnet run --project Server.Auth/Server.Auth.csproj`.
3. In a separate terminal, launch the client: `dotnet run --project Client.Engine/Client.Engine.csproj`.
4. Access Swagger UI at the URL printed in the console (usually `https://localhost:5001/swagger`).

## Testing Strategy
- **Unit Tests** reside under each project's `*.Tests` folder. Run with `dotnet test`.
- **Integration Tests** spin up an in‑memory SQLite or PostgreSQL (via Docker) and hit both REST and gRPC endpoints, verifying end‑to‑end flow.
- Ensure JWT generation and password hashing are exercised in tests.

## Future Work (tracked in `TODO.md`)
- Replace the in‑memory repository with a production‑grade EF Core implementation.
- Harden JWT signing with a real secret/key.
- Add password hashing using BCrypt.
- Implement rate‑limiting middleware for authentication endpoints.

---
*Generated for use by Qwen Code agents to provide consistent guidance across sessions.*