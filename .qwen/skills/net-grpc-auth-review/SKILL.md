---
name: net-grpc-auth-review
description: Code review methodology for .NET gRPC authentication services with security focus
source: auto-skill
extracted_at: '2026-06-08T15:01:53.407Z'
---

# .NET gRPC Auth Service Review Skill

## Purpose
Review .NET 10 gRPC authentication services (ASP.NET Core + EF Core + JWT) for security, correctness, and code quality issues.

## Review Checklist

### Critical Security (always check first)
- [ ] **PIN/password handling**: Stored as salted hash (BCrypt/Argon2), not plaintext
- [ ] **Constant-time comparison**: `CryptographicOperations.FixedTimeEquals` used correctly (not inverted)
- [ ] **Secrets management**: JWT keys, Redis passwords, HMAC keys from env/user-secrets — never hardcoded
- [ ] **Nonce/token validation**: Logic not inverted (valid = accept, invalid = reject)
- [ ] **Rate limiting**: On auth endpoints (login, register, refresh)
- [ ] **Token blacklist/revocation**: Implemented and checked on every request

### Auth Logic Correctness
- [ ] **Registration flow**: Validates registration code, checks duplicate email, creates user + groups atomically
- [ ] **Login flow**: Validates nonce (single-use), verifies PIN hash, issues access+refresh tokens
- [ ] **Refresh flow**: Validates refresh token, rotates tokens, revokes old refresh token
- [ ] **Master key flow**: Groups loaded before permission check
- [ ] **Group assignment**: N+1 queries avoided (batch load groups)

### JWT Implementation
- [ ] **Access token**: Short expiry (15-30 min), contains roles/groups
- [ ] **Refresh token**: Long expiry (1-7 days), stored in DB + Redis blacklist
- [ ] **Validation**: `ValidateLifetime=true`, `ValidateIssuerSigningKey=true`, proper clock skew
- [ ] **Parsing**: Method named `ParseToken` (not `ParceToken`)

### Protobuf Contract
- [ ] **Field names**: No typos (`encrypted_key` not `encrypteded_key`)
- [ ] **Types match**: PIN length validation aligns with proto (bytes vs digits)
- [ ] **Required fields**: Marked appropriately

### Code Quality
- [ ] **N+1 queries**: Batch load related entities
- [ ] **Redundant queries**: Reuse already-loaded navigation properties
- [ ] **Error handling**: Specific exceptions caught, not bare `Exception`
- [ ] **Logging**: Structured, no PII in logs
- [ ] **Comments**: English for public API, Russian OK for internal logic
- [ ] **Naming**: PascalCase public, camelCase private, interfaces prefixed `I`

### EF Core / Database
- [ ] **Indexes**: Unique on Email, proper FK cascades
- [ ] **Migrations**: Up-to-date, no pending
- [ ] **Concurrency**: Version tokens / row version where needed

## Common Bug Patterns (from review experience)

| Pattern | Symptom | Fix |
|---------|---------|-----|
| Inverted `FixedTimeEquals` | Valid PIN returns 401 | `if (!FixedTimeEquals(...))` |
| Inverted nonce check | Valid nonce rejected | `if (!await VerifyNonce(...))` |
| Plaintext PIN storage | `PinCodeHash = request.Pin` | Hash with BCrypt on register |
| Hardcoded JWT key | `"JWT_KEY"` in appsettings | Env var / user secrets |
| N+1 in group loops | `foreach` with `FirstAsync()` | `Where(id in ids).ToList()` |
| Groups checked before load | AuthZ fails | Load groups first or embed in JWT |
| Refresh token stub | Returns "JWT" literal | Full rotation implementation |

## Tools to Run
```bash
dotnet build                    # Compilation check
dotnet test                     # Unit + integration tests
dotnet ef migrations list       # Migration status
```

## Output Format
Group findings by severity:
- **Critical**: Security bugs, logic inversions, data loss
- **Suggestion**: Code quality, performance, maintainability
- **Nice to have**: Style, minor optimizations

Include file:line references, concrete fix suggestions.