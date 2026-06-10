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
        private readonly IOptions<SecurityOptions> _securityOptions;

        public SecurityService(IConnectionMultiplexer redis, ILogger<SecurityService> logger, IOptions<SecurityOptions> securityOptions)
        {
            _redis = redis.GetDatabase().WithKeyPrefix("security:");
            _logger = logger;
            _securityOptions = securityOptions;
            if (_securityOptions.Value.NonceSecretKey == null)
                throw new InvalidOperationException("Nonce secret key is not found.");
        }
        public async Task<string> GetNonceToken()
        {
            string nonce = Guid.NewGuid().ToString();
            long expiration = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
            string token = $"{expiration}.{nonce}";
            string signature = Convert.ToBase64String(HMACSHA256.HashData(_securityOptions.Value.NonceSecretKey, Encoding.UTF8.GetBytes(token)));
            await _redis.StringSetAsync($"nonce:{nonce}", true, TimeSpan.FromMinutes(5));
            
            return $"{token}.{signature}";
        }
        public async Task<bool> VerifyNonceToken(string token)
        {
            if(string.IsNullOrEmpty(token))
            {
                return false;
            }
            string[] parts = token.Split('.');
            if(parts.Length != 3)
            {
                return false;
            }
            if (!long.TryParse(parts[0], out long expiration) || 
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiration)
            {
                return false;
            }
            string nonce = parts[1];
            string signature = parts[2];
            byte[] expectedHash = HMACSHA256.HashData(_securityOptions.Value.NonceSecretKey, Encoding.UTF8.GetBytes($"{expiration}.{nonce}"));
            byte[] providedHash = Convert.FromBase64String(signature);
            if(!CryptographicOperations.FixedTimeEquals(expectedHash,providedHash))
            {
                return false;
            }
            return await _redis.KeyDeleteAsync($"nonce:{nonce}");
        }
        public byte[] GenerateSalt()
        {
            return RandomNumberGenerator.GetBytes(_securityOptions.Value.SaltLength);
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
              DegreeOfParallelism = _securityOptions.Value.Argon2.DegreeOfParallelism,
              Iterations = _securityOptions.Value.Argon2.Iterations,
              MemorySize = _securityOptions.Value.Argon2.MemorySize
            };

            return await argon2.GetBytesAsync(_securityOptions.Value.PasswordHashLength);
        }
    }
}