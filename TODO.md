# TODO List

## Security

- [ ] Replace plaintext password storage with salted hash (InMemoryUserRepository.cs)
- [ ] Implement JWT for authentication tokens (AuthController.cs)
- [ ] Add rate limiting to authentication endpoints (AuthController.cs)
- [ ] Use constant-time comparison for password checks (AuthController.cs)
- [ ] Validate and limit Base64 payload size in middleware (Base64DecodingMiddleware.cs)

## Performance

- [ ] Optimize user lookup in InMemoryUserRepository (consider using ConcurrentDictionary for thread safety and performance)
- [ ] Review and optimize Base64 decoding middleware for large payloads

## Logic

- [ ] Implement proper error handling for edge cases (e.g., null inputs in helpers)
- [ ] Consider adding refresh token flow for gRPC and REST
- [ ] Ensure transactional behavior for user operations if moving to a database

## Notes

- Each TODO should be linked to a specific file and, if possible, a line number or function.
- Prioritize security-related TODOs first.
- When a TODO is addressed, update this file to reflect the resolution.
