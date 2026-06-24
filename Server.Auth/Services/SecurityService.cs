using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Server.Auth.Interfaces;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using Server.Auth.Options;

namespace Server.Auth.Services
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
            string nonce = Guid.NewGuid().ToString();
            long expiration = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
            string token = $"{expiration}.{nonce}";
            string signature = Convert.ToBase64String(HMACSHA256.HashData(_securityOptions.NonceSecretKey, Encoding.UTF8.GetBytes(token)));
            await _redis.StringSetAsync($"nonce:{nonce}", true, TimeSpan.FromMinutes(5));
            return $"{token}.{signature}";
        }

        public async Task<bool> VerifyNonceToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("VerifyNonceToken: токен пуст");
                return false;
            }
            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("VerifyNonceToken: неверный формат токена (частей: {PartsCount})", parts.Length);
                return false;
            }
            if (!long.TryParse(parts[0], out long expiration) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiration)
            {
                _logger.LogWarning("VerifyNonceToken: токен просрочен");
                return false;
            }
            string nonce = parts[1];
            string signature = parts[2];
            byte[] expectedHash = HMACSHA256.HashData(_securityOptions.NonceSecretKey, Encoding.UTF8.GetBytes($"{expiration}.{nonce}"));
            byte[] providedHash = Convert.FromBase64String(signature);
            if (!CryptographicOperations.FixedTimeEquals(expectedHash, providedHash))
            {
                _logger.LogWarning("VerifyNonceToken: невалидная подпись");
                return false;
            }
            bool deleted = await _redis.KeyDeleteAsync($"nonce:{nonce}");
            if (!deleted)
            {
                _logger.LogWarning("VerifyNonceToken: токен уже использован");
            }
            return deleted;
        }

        public byte[] GenerateSalt()
        {
            return RandomNumberGenerator.GetBytes(_securityOptions.SaltLength);
        }

        public async Task<byte[]> HashPasswordAsync(string password, byte[] salt)
        {
            return await HashPasswordAsync(Encoding.UTF8.GetBytes(password), salt);
        }

        public async Task<byte[]> HashPasswordAsync(byte[] password, byte[] salt)
        {
            using var argon2 = new Argon2id(password)
            {
                Salt = salt,
                DegreeOfParallelism = _securityOptions.Argon2.DegreeOfParallelism,
                Iterations = _securityOptions.Argon2.Iterations,
                MemorySize = _securityOptions.Argon2.MemorySize,
            };

            byte[] hash = await argon2.GetBytesAsync(_securityOptions.PasswordHashLength);
            return hash;
        }
    }
}