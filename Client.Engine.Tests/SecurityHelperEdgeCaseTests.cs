using System.Security.Cryptography;
using Client.Engine.Helpers;
using FluentAssertions;

namespace Client.Engine.Tests;

public class SecurityHelperEdgeCaseTests
{
    // ── EncryptPassword null/zero argument guards ──────────────────────────

    [Fact]
    public void EncryptPassword_NullPassword_ThrowsArgumentNullException()
    {
        var key = RandomNumberGenerator.GetBytes(32);

        var act = () => SecurityHelper.EncryptPassword(null!, key, 12, 16, 64);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("password");
    }

    [Fact]
    public void EncryptPassword_NullKey_ThrowsArgumentNullException()
    {
        var act = () => SecurityHelper.EncryptPassword("password", null!, 12, 16, 64);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    [Theory]
    [InlineData(0, 16, 64, "nonceLength")]
    [InlineData(12, 0, 64, "tagLength")]
    [InlineData(12, 16, 0, "encryptedLength")]
    [InlineData(-1, 16, 64, "nonceLength")]
    [InlineData(12, -1, 64, "tagLength")]
    [InlineData(12, 16, -1, "encryptedLength")]
    public void EncryptPassword_ZeroOrNegativeLengths_ThrowsArgumentOutOfRangeException(
        int nonceLength, int tagLength, int encryptedLength, string expectedParam)
    {
        var key = RandomNumberGenerator.GetBytes(32);

        var act = () => SecurityHelper.EncryptPassword("password", key, nonceLength, tagLength, encryptedLength);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(expectedParam);
    }

    // ── EncryptKey null argument guards ────────────────────────────────────

    [Fact]
    public void EncryptKey_NullPassword_ThrowsArgumentNullException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var salt = RandomNumberGenerator.GetBytes(16);

        var act = () => SecurityHelper.EncryptKey(null!, key, salt, 12, 16, 1000, 32);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("password");
    }

    [Fact]
    public void EncryptKey_NullKey_ThrowsArgumentNullException()
    {
        var salt = RandomNumberGenerator.GetBytes(16);

        var act = () => SecurityHelper.EncryptKey("password", null!, salt, 12, 16, 1000, 32);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    [Fact]
    public void EncryptKey_NullSalt_ThrowsArgumentNullException()
    {
        var key = RandomNumberGenerator.GetBytes(32);

        var act = () => SecurityHelper.EncryptKey("password", key, null!, 12, 16, 1000, 32);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("salt");
    }

    // ── DecryptKey: tampered data should fail ──────────────────────────────

    [Fact]
    public void DecryptKey_TamperedCiphertext_ThrowsCryptographicException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var salt = RandomNumberGenerator.GetBytes(16);

        byte[] encrypted = SecurityHelper.EncryptKey("StrongPass1!", key, salt, 12, 16, 1000, 32);

        // Tamper with the ciphertext portion (after nonce + tag)
        encrypted[12 + 16] ^= 0xFF;

        var act = () => SecurityHelper.DecryptKey(encrypted, "StrongPass1!", salt, 12, 16, 1000, 32);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void DecryptKey_NullEncryptedToken_ThrowsArgumentNullException()
    {
        var salt = RandomNumberGenerator.GetBytes(16);

        var act = () => SecurityHelper.DecryptKey(null!, "password", salt, 12, 16, 1000, 32);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("encryptedToken");
    }

    [Fact]
    public void DecryptKey_NullPassword_ThrowsArgumentNullException()
    {
        var encryptedToken = new byte[60];
        var salt = RandomNumberGenerator.GetBytes(16);

        var act = () => SecurityHelper.DecryptKey(encryptedToken, null!, salt, 12, 16, 1000, 32);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("password");
    }

    [Fact]
    public void DecryptKey_NullSalt_ThrowsArgumentNullException()
    {
        var encryptedToken = new byte[60];

        var act = () => SecurityHelper.DecryptKey(encryptedToken, "password", null!, 12, 16, 1000, 32);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("salt");
    }

    [Theory]
    [InlineData(12, 16, 27)]  // nonceLength + tagLength = 28, but token is 27
    [InlineData(12, 16, 1)]   // way too short
    [InlineData(12, 16, 0)]   // empty
    public void DecryptKey_TooShortToken_ThrowsArgumentException(
        int nonceLength, int tagLength, int tokenLength)
    {
        var token = new byte[tokenLength];
        var salt = RandomNumberGenerator.GetBytes(16);

        var act = () => SecurityHelper.DecryptKey(token, "password", salt, nonceLength, tagLength, 1000, 32);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*too short*")
            .WithParameterName("encryptedToken");
    }

    // ── GetSecurityHash determinism and correctness ────────────────────────

    [Fact]
    public void GetSecurityHash_ByteOverload_SameInput_ProducesSameOutput()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("deterministic-input");
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        byte[] hash1 = SecurityHelper.GetSecurityHash(data, salt, 1, 1, 1024, 16);
        byte[] hash2 = SecurityHelper.GetSecurityHash(data, salt, 1, 1, 1024, 16);

        hash2.Should().BeEquivalentTo(hash1, "same inputs must produce identical hashes");
    }

    [Fact]
    public void GetSecurityHash_ByteOverload_DifferentData_ProducesDifferentOutput()
    {
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var data1 = System.Text.Encoding.UTF8.GetBytes("input-alpha");
        var data2 = System.Text.Encoding.UTF8.GetBytes("input-beta");

        byte[] hash1 = SecurityHelper.GetSecurityHash(data1, salt, 1, 1, 1024, 16);
        byte[] hash2 = SecurityHelper.GetSecurityHash(data2, salt, 1, 1, 1024, 16);

        hash2.Should().NotBeEquivalentTo(hash1, "different inputs must produce different hashes");
    }

    [Fact]
    public void GetSecurityHash_StringOverload_NullData_ThrowsArgumentNullException()
    {
        var salt = new byte[] { 1, 2, 3, 4 };

        var act = () => SecurityHelper.GetSecurityHash((string)null!, salt, 1, 1, 1024, 16);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public void GetSecurityHash_StringOverload_NullSalt_ThrowsArgumentNullException()
    {
        var act = () => SecurityHelper.GetSecurityHash("data", null!, 1, 1, 1024, 16);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("salt");
    }

    [Fact]
    public void GetSecurityHash_ByteOverload_NullData_ThrowsArgumentNullException()
    {
        var salt = new byte[] { 1, 2, 3, 4 };

        var act = () => SecurityHelper.GetSecurityHash((byte[])null!, salt, 1, 1, 1024, 16);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public void GetSecurityHash_ByteOverload_NullSalt_ThrowsArgumentNullException()
    {
        var data = new byte[] { 1, 2, 3 };

        var act = () => SecurityHelper.GetSecurityHash(data, null!, 1, 1, 1024, 16);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("salt");
    }

    // ── GenerateRsaKeyPair guards ──────────────────────────────────────────

    [Theory]
    [InlineData(1024)]
    [InlineData(2047)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(512)]
    public void GenerateRsaKeyPair_KeySizeBelow2048_ThrowsArgumentOutOfRangeException(int keySize)
    {
        var act = () => SecurityHelper.GenerateRsaKeyPair(keySize);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("keySize");
    }

    [Fact]
    public void GenerateRsaKeyPair_KeySize2048_ReturnsNonEmptyPemKeys()
    {
        var (publicKey, privateKey) = SecurityHelper.GenerateRsaKeyPair(2048);

        publicKey.Should().NotBeNullOrEmpty().And.Contain("-----BEGIN PUBLIC KEY-----");
        privateKey.Should().NotBeNullOrEmpty().And.Contain("-----BEGIN PRIVATE KEY-----");
    }

    // ── EncryptBackupKey guards ────────────────────────────────────────────

    [Fact]
    public void EncryptBackupKey_NullPublicKeyPem_ThrowsArgumentNullException()
    {
        var key = RandomNumberGenerator.GetBytes(32);

        var act = () => SecurityHelper.EncryptBackupKey(null!, key);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("publicKeyPem");
    }

    [Fact]
    public void EncryptBackupKey_NullKey_ThrowsArgumentNullException()
    {
        var (publicKey, _) = SecurityHelper.GenerateRsaKeyPair(2048);

        var act = () => SecurityHelper.EncryptBackupKey(publicKey, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    [Fact]
    public void EncryptBackupKey_EmptyPem_ThrowsArgumentException()
    {
        var key = RandomNumberGenerator.GetBytes(32);

        var act = () => SecurityHelper.EncryptBackupKey("", key);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("publicKeyPem");
    }

    [Fact]
    public void EncryptBackupKey_InvalidPem_ThrowsException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var invalidPem = "this is not a valid PEM key";

        var act = () => SecurityHelper.EncryptBackupKey(invalidPem, key);

        act.Should().Throw<Exception>();
    }
}