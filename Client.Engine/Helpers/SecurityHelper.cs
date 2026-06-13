
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
                encryptor.Decrypt(nonce, encryptedKey, decryptedKey, tag);
                CryptographicOperations.ZeroMemory(passwordBytes);
                CryptographicOperations.ZeroMemory(passwordHash);
                return decryptedKey;
            }
        }
        public static byte[] GetSecurityHash(byte[] data, byte[] salt, int degreesOfParallelism, int iterations, int memorySize, int hashLength)
        {
            using var argon2 = new Argon2id(data)
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
            CryptographicOperations.ZeroMemory(data);
            return result;
        }
        public static byte[] GetSecurityHash(string data, byte[] salt, int degreesOfParallelism, int iterations, int memorySize, int hashLength)
        {
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
            using (RSA rsa = RSA.Create(keySize))
            {
                return (rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
            }
        }
        public static byte[] EncryptBackupKey(string publicKeyPem, byte[] key)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportFromPem(publicKeyPem);
                return rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256);
            }
        }
    }
}