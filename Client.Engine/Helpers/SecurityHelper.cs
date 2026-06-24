
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;

namespace Client.Engine.Helpers
{
    public static class SecurityHelper
    {
        public static byte[] EncryptPassword(string password, byte[] key, int nonceLength, int tagLength, int encryptedLength)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (nonceLength <= 0) throw new ArgumentOutOfRangeException(nameof(nonceLength));
            if (tagLength <= 0) throw new ArgumentOutOfRangeException(nameof(tagLength));
            if (encryptedLength <= 0) throw new ArgumentOutOfRangeException(nameof(encryptedLength));
            using (var encryptor = new AesGcm(key, tagLength))
            {
                byte[] nonce = RandomNumberGenerator.GetBytes(nonceLength);
                byte[] passwordBytesPadded = new byte[encryptedLength];
                System.Text.Encoding.UTF8.GetBytes(password, passwordBytesPadded);
                byte[] tag = new byte[tagLength];
                byte[] encryptedPassword = new byte[encryptedLength];
                encryptor.Encrypt(nonce, passwordBytesPadded, encryptedPassword, tag);
                byte[] result = new byte[nonceLength + tagLength + encryptedPassword.Length];
                Buffer.BlockCopy(nonce, 0, result, 0, nonceLength);
                Buffer.BlockCopy(tag, 0, result, nonceLength, tagLength);
                Buffer.BlockCopy(encryptedPassword, 0, result, nonceLength + tagLength, encryptedPassword.Length);
                return result;
            }
        }
        public static byte[] EncryptKey(string password, byte[] key, byte[] salt, int nonceLength, int tagLength, int iterations, int derivedKeyLength)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (salt == null) throw new ArgumentNullException(nameof(salt));
            byte[] nonce = RandomNumberGenerator.GetBytes(nonceLength);
            byte[] tag = new byte[tagLength];
            byte[] encryptedKey = new byte[key.Length];
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            byte[] passwordHash = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA256, derivedKeyLength);
            using (var encryptor = new AesGcm(passwordHash, tagLength))
            {
                encryptor.Encrypt(nonce, key, encryptedKey, tag);
                byte[] result = new byte[nonceLength + tagLength + key.Length];
                Buffer.BlockCopy(nonce, 0, result, 0, nonceLength);
                Buffer.BlockCopy(tag, 0, result, nonceLength, tagLength);
                Buffer.BlockCopy(encryptedKey, 0, result, nonceLength + tagLength, key.Length);
                CryptographicOperations.ZeroMemory(passwordBytes);
                CryptographicOperations.ZeroMemory(passwordHash);
                CryptographicOperations.ZeroMemory(encryptedKey);
                return result;
            }
        }

        public static byte[] DecryptKey(byte[] encryptedToken, string password, byte[] salt, int nonceLength, int tagLength, int iterations, int derivedKeyLength)
        {
            if (encryptedToken == null) throw new ArgumentNullException(nameof(encryptedToken));
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (salt == null) throw new ArgumentNullException(nameof(salt));
            if (encryptedToken.Length < nonceLength + tagLength) throw new ArgumentException("Encrypted token is too short.", nameof(encryptedToken));
            byte[] nonce = new byte[nonceLength];
            byte[] tag = new byte[tagLength];
            byte[] encryptedKey = new byte[encryptedToken.Length - nonceLength - tagLength];
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            byte[] passwordHash = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA256, derivedKeyLength);
            using (var encryptor = new AesGcm(passwordHash, tagLength))
            {
                Buffer.BlockCopy(encryptedToken, 0, nonce, 0, nonceLength);
                Buffer.BlockCopy(encryptedToken, nonceLength, tag, 0, tagLength);
                Buffer.BlockCopy(encryptedToken, nonceLength + tagLength, encryptedKey, 0, encryptedKey.Length);
                byte[] decryptedKey = new byte[encryptedKey.Length];
                encryptor.Decrypt(nonce, encryptedKey, tag, decryptedKey);
                CryptographicOperations.ZeroMemory(passwordBytes);
                CryptographicOperations.ZeroMemory(passwordHash);
                return decryptedKey;
            }
        }
        public static byte[] GetSecurityHash(byte[] data, byte[] salt, int degreesOfParallelism, int iterations, int memorySize, int hashLength)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (salt == null) throw new ArgumentNullException(nameof(salt));
            byte[] localData = new byte[data.Length];
            Buffer.BlockCopy(data, 0, localData, 0, data.Length);
            using var argon2 = new Argon2id(localData)
            {
                Salt = salt,
                DegreeOfParallelism = degreesOfParallelism,
                Iterations = iterations,
                MemorySize = memorySize
            };
            byte[] dataHash = argon2.GetBytes(hashLength);
            byte[] result = new byte[salt.Length + hashLength];
            Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
            Buffer.BlockCopy(dataHash, 0, result, salt.Length, hashLength);
            CryptographicOperations.ZeroMemory(localData);
            return result;
        }
        public static byte[] GetSecurityHash(string data, byte[] salt, int degreesOfParallelism, int iterations, int memorySize, int hashLength)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (salt == null) throw new ArgumentNullException(nameof(salt));
            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(data);
            return GetSecurityHash(dataBytes, salt, degreesOfParallelism, iterations, memorySize, hashLength);
        }
        /// <summary>
        /// Генерирует пару ключей RSA с заданным размером ключа.
        /// </summary>
        /// <param name="keySize">Размер ключа в битах.</param>
        /// <returns>Кортеж с публичным и приватным ключами RSA.</returns>
        public static (string, string) GenerateRsaKeyPair(int keySize)
        {
            if (keySize < 2048) throw new ArgumentOutOfRangeException(nameof(keySize), "Key size must be at least 2048 bits.");
            using (RSA rsa = RSA.Create(keySize))
            {
                return (rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
            }
        }
        public static byte[] EncryptBackupKey(string publicKeyPem, byte[] key)
        {
            if (publicKeyPem == null) throw new ArgumentNullException(nameof(publicKeyPem));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (publicKeyPem.Length == 0) throw new ArgumentException("Public key PEM cannot be empty.", nameof(publicKeyPem));
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportFromPem(publicKeyPem);
                return rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256);
            }
        }
    }
}
