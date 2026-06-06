# Project Guidelines for DataGuard Project

## General

- Follow the existing .NET 10 conventions in the codebase.
- Ensure security best practices are followed, especially regarding password handling and token generation.
- Maintain compatibility between REST and gRPC responses (Base64-encoded protobuf).
- Don't do more than you've been asked to do.

## Code Style

- Write all instructions and documentation in English.
- Use PascalCase for public members, camelCase for private and local variables.
- Prefix interfaces with 'I'.
- Async methods should be suffixed with 'Async'.
- Keep methods focused and single-responsibility.

## Security

- **Importance of security**: Always prioritize security in code; vulnerabilities can lead to data breaches and loss of trust.
- Never store or log plaintext passwords.
- Use strong, salted password hashing (e.g., BCrypt, PBKDF2).
- Use JWT or other secure token mechanism for authentication (not plain GUID).
- Implement rate limiting on authentication endpoints.
- Use constant-time comparison for password hashes.

## Comments and Documentation

- Write comments for functions in Russian language to explain the purpose, parameters, return values,and any side effects.
- Leave TODO comments for functions that need future fixes (performance, security, or logic). These will be tracked in TODO.md.

## Testing

- Write unit tests for all new logic.
- Integration tests should cover REST and gRPC endpoints.
- Mock external dependencies (like repositories) in unit tests.

## Development

- Run `dotnet test` to ensure all tests pass before committing.
- Use `dotnet run` to start the application and check Swagger UI at /swagger.
- gRPC service is available on port 8081.
- Database is PostgreSQL, managed via docker-compose.yml.

## Project Structure

- Controllers: Handle HTTP requests and map to services.
- Services: Contain business logic.
- Interfaces: Define contracts for services (especially repositories).
- Models: DTOs for request/response bodies.
- Protos: gRPC service definitions.
- Middlewares: Custom ASP.NET Core middleware.
- Helpers: Utility classes.
- Repositories: Data access layer using Entity Framework Core with PostgreSQL.
- Contexts: Database contexts for EF Core.

## Important Files

- SECURITY.md: Details identified threats and mitigations.
- AGENTS.md: Repository guidelines for AI agents.
- TODO.md: Tracks TODO comments categorized by importance (performance, security, logic).

## Instructions for AGENTS.md Updates

- When adding new instructions to AGENTS.md, either keep the instruction directly in this file or add a reference to a separate file if the instruction is situational.
- For example, if an instruction applies only to a specific module, create a separate file (e.g., AUTH_GUIDELINES.md) and reference it here.
