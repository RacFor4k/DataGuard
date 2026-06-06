# Security Guidelines

## Identified Threats and Mitigations

- **Plaintext password storage**: 
  - File: `Server/Services/InMemoryUserRepository.cs`, method `Add` stores password as-is
  - File: `Server/Controllers/AuthController.cs`, methods `Register` and `Login` handle plain passwords
  - Mitigation: Replace with salted hash using BCrypt/PBKDF2; never store or transmit plain passwords

- **Weak authentication token**:
  - File: `Server/Controllers/AuthController.cs`, method `Login` uses `Guid.NewGuid()` for token
  - Mitigation: Implement JWT with secure signing key and expiration

- **User enumeration via registration**:
  - File: `Server/Controllers/AuthController.cs`, method `Register` returns specific error when email exists
  - Mitigation: Return generic message for both new and existing emails (e.g., "Registration successful or email already exists")

- **Lack of rate limiting**:
  - File: `Server/Controllers/AuthController.cs`, endpoints `Register` and `Login`
  - Mitigation: Add rate limiting middleware (e.g., AspNetCoreRateLimit) to prevent brute force attacks

- **Potential timing attack in password comparison**:
  - File: `Server/Controllers/AuthController.cs`, method `Login` uses `!=` operator on strings
  - Mitigation: Use cryptographic constant-time comparison (e.g., `CryptographicOperations.FixedTimeEquals`)

- **Unrestricted input size on Base64 payload**:
  - File: `Server/Middlewares/Base64DecodingMiddleware.cs` (inferred from README)
  - Mitigation: Implement request size limits and validate payload length before decoding

- **Missing HTTPS enforcement**:
  - File: Deployment configuration (not in codebase)
  - Mitigation: Enable `RequireHttpsAttribute` or middleware in production; use HSTS

## Addressed Concerns

- **Password leakage in error messages**: 
  - Login endpoint returns generic "Неверный email или пароль." for both invalid email and password, mitigating user enumeration via login.
  - Location: `Server/Controllers/AuthController.cs`, method `Login`
