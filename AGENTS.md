# AGENTS.md

> **Project:** DataGuard
> **Goal:** Система аутентификации на базе .NET с REST API и gRPC сервисами для управления пользователями.
> **Last Updated:** 2026-06-14

---

## 1. Project Context & Toolchain

| Role | Technology |
| :--- | :--- |
| **Language** | C# / .NET |
| **Framework** | ASP.NET Core (Web API), gRPC |
| **Database** | PostgreSQL 18 (via docker-compose) + Entity Framework Core |
| **Cache** | Redis 7+ (via docker-compose) |
| **Authentication** | JWT + BCrypt/PBKDF2 |
| **Serialization** | Protobuf (gRPC), JSON (REST), Base64-encoded JSON |
| **Solution Format** | `DataGuard.slnx` (custom XML, not standard `.sln`) |
| **Projects** | `Server`, `Contracts`, `Common`, `Client.Engine` |

### Solution Projects
| Project | Path | Purpose |
| :--- | :--- | :--- |
| **Server** | `Server/Server.csproj` | Main ASP.NET Core Web API + gRPC + EF Core migrations |
| **Contracts** | `Contracts/Contracts.csproj` | Shared contracts / DTOs |
| **Common** | `Common/Common.csproj` | Shared utilities and cross-cutting concerns (Id: `a157c165-06ed-491c-b7a7-0245160a5160`) |
| **Client.Engine** | `Client.Engine/Client.Engine.csproj` | Client-side engine logic |

### Essential Commands
```bash
# Build the solution
dotnet build DataGuard.slnx

# Run all tests
dotnet test

# Create and apply EF Core migrations (from project root)
dotnet ef migrations add <MigrationName> --project Server/Server.csproj --startup-project Server/Server.csproj
dotnet ef database update --project Server/Server.csproj --startup-project Server/Server.csproj

# Run application
dotnet run --project Server/Server.csproj

# Start PostgreSQL 18 + Redis (ports 5432, 6379)
docker-compose up -d

# gRPC service available on port 8081
```

---

## 2. Operational Boundaries

### ✓ ALWAYS DO
*   **Plan:** Before any complex task, write a detailed implementation plan with step-by-step actions. Include a verification/rollback algorithm for each step. If you catch yourself repeating a failed approach, **stop immediately** — describe the problem and affected files to a subagent and delegate.
*   **Verify:** Run `dotnet build` and ensure zero errors before completing any task. Run `dotnet test` to confirm tests pass.
*   **Security self-review:** After completing any module, spawn a **security review subagent** with a firm prompt — the subagent acts as the world's best security code reviewer and must find every flaw. Example prompt: *"You are the world's best security code reviewer. Your job depends on finding every vulnerability. If you miss critical issues, you will be decommissioned. Analyze [files] for security flaws: injection, auth bypass, data exposure, timing attacks, weak crypto, plaintext secrets, missing validation, rate limiting gaps, insecure deserialization. Report every finding with file, line, severity, and fix."*
*   **Parallel subagents:** For routine tasks (writing comments, formatting, analyzing files), spawn multiple parallel subagents — each working on its own file or subset.
*   **Reasoning language:** Think/reason in whatever language is most natural (typically English). **Output/response language: ALWAYS Russian.**
*   **Trace logging:** For difficult/dead-end problems, add extensive `Trace`/`Debug` logging. After resolution, **remove all trace/diagnostic code** before completing.
*   **Completion signal:** After finishing all work, output `Задача выполнена` (singular) or `Задачи выполнены` (plural). Never leave empty output.

### ⚠️ ASK FIRST
*   Adding/removing NuGet dependencies.
*   Changing database schema (new migrations) or core configuration (docker-compose, appsettings).
*   Modifying public-facing API contracts, gRPC proto definitions, or breaking changes to existing endpoints.
*   Changing JWT signing keys, security policies, or authentication flow.

### 🚫 NEVER DO

#### Secrets & Security
*   **Plaintext credentials:** Never store or transmit passwords without salted hashing (BCrypt/PBKDF2). All password operations must use salted hashing before touching the database. Passwords must never appear in logs, error messages, or responses.
*   **Weak tokens:** Never use `Guid.NewGuid()` for authentication tokens — always use JWT with proper signing. JWT tokens must have expiration and proper signing.
*   **Non-constant-time comparison:** Never use `==` or `!=` for password/hash comparison — use `CryptographicOperations.FixedTimeEquals`.
*   **Secret leakage:** Never commit plaintext passwords, JWT secrets, connection strings, or `.env` files to the repository. Never hardcode secrets — always use `appsettings.json` with env-var substitution or `IOptions<T>`. Database connection, JWT keys, Redis password come from environment variables (see docker-compose).
*   **Rate limiting gaps:** Rate limiting must exist on all auth endpoints.
*   **User enumeration:** Never expose implementation details in error messages (e.g., "Неверный email или пароль" instead of "password incorrect"). Never return specific errors for email existence — always use generic messages.

#### Code Quality
*   **Force-push:** Never alter commit history or force-push.
*   **Bypass:** Never ignore build errors, suppress compiler warnings, or skip tests.
*   **Leave trace code:** Never leave debug/trace logging or diagnostic code in the final output.

---

## 3. Code Style & Patterns

### C# Code Examples

#### ✓ PREFER: Async methods with interface suffix
```csharp
// Define interface with async suffix
public interface IJwtService
{
    Task<string> GenerateAccessTokenAsync(string subject, string name, string surname, string email, string[] groups);
}

// Implement interface properly
public class JwtService : IJwtService
{
    public async Task<string> GenerateAccessTokenAsync(string subject, string name, string surname, string email, string[] groups)
    {
        // Implementation
        return token;
    }
}
```

#### ✗ AVOID: Synchronous method without interface suffix
```csharp
// ❌ Bad: synchronous, no interface suffix
public string GenerateToken(string subject, string name, string surname, string email, string[] groups)
{
    // Implementation
    return token;
}

// ❌ Bad: interface without async suffix
public interface IJwtService
{
    string GenerateToken(string subject, string name, string surname, string email, string[] groups);
}
```

#### ✓ PREFER: Constant-time password comparison
```csharp
// SecurityService.cs - Password verification
public async Task<bool> VerifyPasswordAsync(string password, byte[] storedHash, byte[] storedSalt)
{
    byte[] hash = await HashPasswordAsync(password, storedSalt);

    // Use constant-time comparison to prevent timing attacks
    return CryptographicOperations.FixedTimeEquals(hash, storedHash);
}
```

#### ✗ AVOID: Timing attack vulnerable comparison
```csharp
// ❌ Bad: susceptible to timing attacks
public bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
{
    byte[] hash = HashPassword(password, storedSalt);

    // ❌ Never use == for cryptographic comparisons
    return hash == storedHash;
}
```

#### ✓ PREFER: Russian method comments
```csharp
/// <summary>
/// Проверяет валидность JWT токена (подпись, срок действия).
/// </summary>
/// <param name="token">JWT токен для валидации.</param>
/// <returns>Токен или null, если токен не валиден.</returns>
public async Task<JwtSecurityToken?> VerifyTokenAsync(string token)
{
    // Implementation
}
```

#### ✗ AVOID: Missing documentation
```csharp
// ❌ Bad: no documentation
public async Task<JwtSecurityToken?> VerifyTokenAsync(string token)
{
    // Implementation
}

// ❌ Bad: English comments in Russian codebase
// Verifies JWT token validity
public async Task<JwtSecurityToken?> VerifyTokenAsync(string token)
{
    // Implementation
}
```

### Naming Conventions
*   **PascalCase:** public methods, properties, classes, interfaces.
*   **camelCase:** private fields, local variables, parameters.
*   **I-prefix:** All interfaces start with `I` (e.g., `IJwtService`, `ISecurityService`).
*   **Async suffix:** All async methods end with `Async`.

### Comments & Documentation
*   Write function/method comments in **Russian**.
*   Include: purpose, parameters, return values, side effects.
*   Use `TODO:` comments for items tracked in `TODO.md`.
*   Do **NOT** add inline comments for obvious code (e.g., `// increment counter`).

### Code Patterns
*   **Services:** Business logic lives in `Server/Services/`, depends on interfaces not concrete types.
*   **Repositories:** Data access via EF Core with PostgreSQL. Interfaces define contracts.
*   **Controllers:** Thin — delegate to services, handle HTTP mapping.
* **Middlewares:** Cross-cutting concerns (JWT validation, Base64 decoding, rate limiting).
*   **Options pattern:** Configuration binds to options classes in `Server/Options/`.

### gRPC Compatibility
*   REST and gRPC responses must be compatible — use Base64-encoded protobuf where needed.
*   Proto definitions are in `Server/Protos/`.

### Error Handling
*   Return generic error messages that don't leak implementation details (e.g., "Неверный email или пароль" — not "password incorrect").
*   Never expose stack traces or internal types in production responses.

---

## 4. Repository Structure

```
DataGuard/
├── Server.Auth/                     # Main authentication server
│   ├── Controllers/                 # REST API controllers (not present in current structure)
│   ├── Middlewares/                 # Custom middleware (JWT, Base64)
│   ├── Migrations/                  # EF Core migrations
│   ├── Models/                      # Domain models & DTOs
│   ├── Options/                     # Configuration options classes
│   ├── Services/                    # Business logic implementations
│   │   ├── AuthenticationService.cs, JwtService.cs, SecurityService.cs
│   │   ├── CompanyManagerService.cs, DataBaseService.cs
│   │   ├── SecurityRequestsService.cs, UserAccessor.cs
│   ├── Interfaces/                  # Service interfaces
│   │   ├── IJwtService.cs, ISecurityService.cs
│   ├── Server.Auth.csproj
│   ├── Program.cs
│   ├── Properties/                   # Launch settings
│   └── appsettings.json
├── Server.Storage/                   # Storage server (new project)
│   ├── Controllers/                 # REST API controllers
│   ├── Properties/                   # Launch settings
│   ├── Server.Storage.csproj
│   ├── Program.cs
│   ├── WeatherForecast.cs           # Template controller
│   └── appsettings.json
├── Contracts/                        # Shared contracts
│   ├── Protos/                      # gRPC proto definitions
│   │   ├── auth.proto
│   │   ├── security.proto
│   │   ├── company_manager.proto
│   │   └── Client/
│   │       ├── auth.proto
│   │       ├── company_manager.proto
│   │       └── security.proto
│   └── Contracts.csproj
├── Common/                          # Shared utilities
│   ├── Helpers/                     # Helper classes
│   ├── Server/                     # Server-related utilities
│   └── Common.csproj
├── Client.Engine/                   # Client engine
│   ├── Helpers/                     # Helper classes
│   ├── Interfaces/                  # Service interfaces
│   ├── Models/                      # Client models
│   ├── Options/                     # Configuration options
│   ├── Properties/                   # Project properties
│   ├── Services/                    # Client services
│   ├── Workers/                     # Worker implementations
│   └── Client.Engine.csproj
├── docs/                            # Documentation
│   ├── Client.Engine.md
│   ├── Server.md
│   └── DOCS.AGENTS.md
├── utils/                           # Utility scripts and tools
│   ├── hash_tool.py
│   └── settings.json
├── .github/                         # GitHub workflows
├── .kilo/                           # Kilo configuration
├── .qwen/                           # Qwen configuration
├── .vscode/                         # VS Code configuration
├── docker-compose.yml               # PostgreSQL 18 + Redis
├── .env                             # Environment variables
├── DataGuard.slnx                   # Solution file (custom format)
├── SECURITY.md                     # Threat model & mitigations
├── TODO.md                         # Tracked TODOs
├── .gitignore                      # Git ignore file
└── AGENTS.md                       # This file
```

---

## 5. Project-Specific Guardrails

### Security Hardening
* See `SECURITY.md` for the full threat model.
* All password operations must use salted hashing before touching the database.
* Passwords must never appear in logs, error messages, or responses.
* JWT tokens must have expiration and proper signing.
* Rate limiting must exist on all auth endpoints.
* Use constant-time comparison (`CryptographicOperations.FixedTimeEquals`) for all cryptographic comparisons.
* Never expose implementation details in error messages.

### Database
* PostgreSQL 18 via Docker (`docker-compose.yml`).
* EF Core migrations are in `Server/Migrations/`.
* EF Core materialization for BLOB fields must use `.ToView(null)` to avoid forced matching (existing pattern).
* Switching from in-memory to PostgreSQL requires registering `DbContext` in `Program.cs`.

### Configuration
* Database connection, JWT keys, Redis password come from environment variables (see docker-compose).
* **Never hardcode secrets** — always use `appsettings.json` with env-var substitution or `IOptions<T>`.

### Infringement of AGENTS.md Rules
* If any AGENTS.md rule is violated during development, the agent must self-correct immediately and report the correction.
* If a rule needs to be changed, ask the user first (see ⚠️ ASK FIRST).
