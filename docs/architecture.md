# Architecture

High-level design, component boundaries, runtime flows.

## Overview

DataGuard is a multi-service .NET 10 application:

- `Server` — ASP.NET Core host exposing REST controllers and gRPC services.
- `Contracts` — shared contracts including Protobuf definitions.
- `Common` — shared helpers/utilities.
- `Client.Engine` — background worker/consumer.

Infrastructure dependencies:

- PostgreSQL via Entity Framework Core.
- Redis via StackExchange.Redis.

## Server startup (observed)

From [Server/Program.cs](Server/Program.cs):

- Registers `IConnectionMultiplexer` for Redis.
- Registers `DataGuardDbContext` with Npgsql and snake_case naming.
- Binds `JwtOptions` and `CompanyManagerOptions`.
- Registers `IJwtService`, `ISecurityService`, `UserAccessor`.
- Adds REST controllers and gRPC.
- Maps gRPC services:
  - `AuthenticationService`
  - `SecurityRequestsService`
- Applies `JwtMiddleware` before endpoints.

## Communication layers

- REST — standard ASP.NET Core controller endpoints.
- gRPC — Protocol Buffers based services; expected to be Base64-compatible with REST where applicable (per repo guidelines).

## Security

See [security.md](security.md) for threat model and mitigations.