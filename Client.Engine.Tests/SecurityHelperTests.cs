using System.Security.Cryptography;
using Client.Engine.Helpers;

namespace Client.Engine.Tests;

public class SecurityHelperTests
{
    [Fact]
    public void EncryptKey_ThenDecryptKey_ReturnsOriginalKey()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] salt = RandomNumberGenerator.GetBytes(16);

        byte[] encrypted = SecurityHelper.EncryptKey("StrongPass1!", key, salt, 12, 16, 1000, 32);
        byte[] decrypted = SecurityHelper.DecryptKey(encrypted, "StrongPass1!", salt, 12, 16, 1000, 32);

        Assert.Equal(key, decrypted);
        Assert.Equal(60, encrypted.Length);
    }

    [Fact]
    public void EncryptPassword_ReturnsNonceTagAndCiphertext()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        byte[] encrypted = SecurityHelper.EncryptPassword("StrongPass1!", key, 12, 16, 64);

        Assert.Equal(92, encrypted.Length);
    }

    [Fact]
    public void GetSecurityHash_ReturnsSaltPrefixedHash()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(8);

        byte[] hash = SecurityHelper.GetSecurityHash("value", salt, 1, 1, 1024, 16);

        Assert.Equal(24, hash.Length);
        Assert.Equal(salt, hash[..8]);
    }

    [Fact]
    public void RsaBackupKey_CanBeEncryptedAndDecrypted()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        var (publicKey, privateKey) = SecurityHelper.GenerateRsaKeyPair(2048);

        byte[] encrypted = SecurityHelper.EncryptBackupKey(publicKey, key);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        byte[] decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.OaepSHA256);

        Assert.Equal(key, decrypted);
    }
}