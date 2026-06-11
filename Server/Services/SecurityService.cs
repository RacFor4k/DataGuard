using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Server.Interfaces;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using Server.Options;

namespace Server.Services
{
    public class SecurityService : ISecurityService
    {
        private readonly IDatabase _redis;
        private readonly ILogger<SecurityService> _logger;
        private readonly SecurityOptions _securityOptions;
        public SecurityService(IConnectionMultiplexer redis, ILogger<SecurityService> logger, IOptions<SecurityOptions> securityOptions)
        {
            _redis = redis.GetDatabase().WithKeyPrefix("security:");
            _logger = logger;
            _securityOptions = securityOptions.Value;
        }
        public async Task<string> GetNonceToken()
        {
            _logger.LogTrace($"GetNonceToken called");
            string nonce = Guid.NewGuid().ToString();
            long expiration = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
            string token = $"{expiration}.{nonce}";
            string signature = Convert.ToBase64String(HMACSHA256.HashData(_securityOptions.NonceSecretKey, Encoding.UTF8.GetBytes(token)));
            _logger.LogTrace($"Nonce token generated (nonce: {nonce}, expiration: {expiration})");
            await _redis.StringSetAsync($"nonce:{nonce}", true, TimeSpan.FromMinutes(5));
            _logger.LogTrace($"Nonce token saved in Redis (nonce: {nonce})");
            return $"{token}.{signature}";
        }
        public async Task<bool> VerifyNonceToken(string token)
        {
            _logger.LogTrace($"VerifyNonceToken called (token: {token})");
            if(string.IsNullOrEmpty(token))
            {
                _logger.LogWarning($"VerifyNonceToken failed - token is empty");
                return false;
            }
            string[] parts = token.Split('.');
            if(parts.Length != 3)
            {
                _logger.LogWarning($"VerifyNonceToken failed - token format is invalid (parts: {parts.Length}, expected: 3, token: {token})");
                return false;
            }
            if (!long.TryParse(parts[0], out long expiration) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiration)
            {
                _logger.LogWarning($"VerifyNonceToken failed - token is expired (expiration: {expiration}, currentTime: {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}, token: {token})");
                return false;
            }
            string nonce = parts[1];
            string signature = parts[2];
            _logger.LogTrace($"Parsing nonce token (nonce: {nonce}, signature: {signature})");
            byte[] expectedHash = HMACSHA256.HashData(_securityOptions.NonceSecretKey, Encoding.UTF8.GetBytes($"{expiration}.{nonce}"));
            byte[] providedHash = Convert.FromBase64String(signature);
            if(!CryptographicOperations.FixedTimeEquals(expectedHash,providedHash))
            {
                _logger.LogWarning($"VerifyNonceToken failed - signature is invalid (nonce: {nonce})");
                return false;
            }
            bool deleted = await _redis.KeyDeleteAsync($"nonce:{nonce}");
            if(deleted)
            {
                _logger.LogTrace($"Nonce token verified successfully and deleted (nonce: {nonce})");
            }
            else
            {
                _logger.LogWarning($"Nonce token verified successfully but not deleted (nonce: {nonce})");
            }
            return deleted;
        }
        public byte[] GenerateSalt()
        {
            _logger.LogTrace($"GenerateSalt called (saltLength: {_securityOptions.SaltLength})");
            byte[] salt = RandomNumberGenerator.GetBytes(_securityOptions.SaltLength);
            _logger.LogTrace($"Salt generated successfully (saltLength: {_securityOptions.SaltLength})");
            return salt;
        }
        public async Task<byte[]> HashPasswordAsync(string password, byte[] salt)
        {
            _logger.LogTrace($"HashPasswordAsync called (passwordLength: {password.Length}, saltLength: {salt.Length})");
            return await HashPasswordAsync(Encoding.UTF8.GetBytes(password), salt);
        }
        public async Task<byte[]> HashPasswordAsync(byte[] password, byte[] salt)
        {
            _logger.LogTrace($"HashPasswordAsync called (passwordLength: {password.Length}, saltLength: {salt.Length}, argon2Parallelism: {_securityOptions.Argon2.DegreeOfParallelism}, argon2Iterations: {_securityOptions.Argon2.Iterations}, argon2Memory: {_securityOptions.Argon2.MemorySize})");
            using var argon2 = new Argon2id(password)
            {
              Salt = salt,
              DegreeOfParallelism = _securityOptions.Argon2.DegreeOfParallelism,
              Iterations = _securityOptions.Argon2.Iterations,
              MemorySize = _securityOptions.Argon2.MemorySize,
            };

            byte[] hash = await argon2.GetBytesAsync(_securityOptions.PasswordHashLength);
            _logger.LogTrace($"Password hashed successfully (passwordLength: {password.Length}, saltLength: {salt.Length}, hashLength: {hash.Length})");
            return hash;
        }
    }
}