# Client.Engine

## Purpose

Console worker service for background processing and message queue consumption.

## Components

- `Program.cs` — entry point, DI setup, hosted service startup.
- `QueueProcessorWorker` — background service processing message queue.
- `JwtToken` — model for holding JWT credentials.
- `JwtHelper` — shared helper for JWT operations (from `Common`).

## Configuration

Configured via `appsettings.json` and `appsettings.Development.json`.

Key settings:
- Message queue connection string
- API endpoint
- JWT credentials for service authentication

## Running

```bash
dotnet run --project Client.Engine
```

As Windows service (production):
```bash
sc create DataGuardClientEngine binPath= "C:\path\to\Client.Engine.exe"