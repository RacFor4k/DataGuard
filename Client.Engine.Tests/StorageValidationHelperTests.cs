using System.Security.Cryptography;
using Client.Engine.Helpers;

namespace Client.Engine.Tests;

public class StorageValidationHelperTests
{
    [Fact]
    public void ValidateFileName_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateFileName(""));
    }

    [Fact]
    public void ValidateFileName_Whitespace_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateFileName("   "));
    }

    [Fact]
    public void ValidateFileName_ContainsSlash_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateFileName("file/name.txt"));
    }

    [Fact]
    public void ValidateFileName_ContainsBackslash_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateFileName("file\\name.txt"));
    }

    [Fact]
    public void ValidateFileName_TooLong_Throws()
    {
        var longName = new string('a', 1025);
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateFileName(longName));
    }

    [Fact]
    public void ValidateFileName_Valid_Returns()
    {
        StorageValidationHelper.ValidateFileName("valid_file.txt");
    }

    [Fact]
    public void ValidatePath_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidatePath(""));
    }

    [Fact]
    public void ValidatePath_Traversal_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidatePath("../etc/passwd"));
    }

    [Fact]
    public void ValidatePath_Absolute_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidatePath("/absolute/path"));
    }

    [Fact]
    public void ValidatePath_DriveLetter_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidatePath("C:path"));
    }

    [Fact]
    public void ValidatePath_TooLong_Throws()
    {
        var longPath = new string('a', 4097);
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidatePath(longPath));
    }

    [Fact]
    public void ValidatePath_Valid_Returns()
    {
        StorageValidationHelper.ValidatePath("docs/projects");
    }

    [Fact]
    public void ValidateMetadata_ReservedKeyStorageKey_Throws()
    {
        var metadata = new Dictionary<string, string> { { "storageKey", "value" } };
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_ReservedKeyOwnerId_Throws()
    {
        var metadata = new Dictionary<string, string> { { "ownerId", "value" } };
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_ReservedKeyPhysicalPath_Throws()
    {
        var metadata = new Dictionary<string, string> { { "physicalPath", "value" } };
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_ReservedKeyBucketName_Throws()
    {
        var metadata = new Dictionary<string, string> { { "bucketName", "value" } };
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_ReservedPrefixDoubleUnderscore_Throws()
    {
        var metadata = new Dictionary<string, string> { { "__internal", "value" } };
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_TooManyKeys_Throws()
    {
        var metadata = new Dictionary<string, string>();
        for (int i = 0; i < 65; i++)
        {
            metadata[$"key{i}"] = "value";
        }
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_EmptyKey_Throws()
    {
        var metadata = new Dictionary<string, string> { { "", "value" } };
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_KeyTooLong_Throws()
    {
        var longKey = new string('k', 257);
        var metadata = new Dictionary<string, string> { { longKey, "value" } };
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_ValueTooLong_Throws()
    {
        var longValue = new string('v', 4097);
        var metadata = new Dictionary<string, string> { { "key", longValue } };
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateMetadata(metadata));
    }

    [Fact]
    public void ValidateMetadata_Valid_Returns()
    {
        var metadata = new Dictionary<string, string>
        {
            { "author", "user1" },
            { "version", "1.0" }
        };
        StorageValidationHelper.ValidateMetadata(metadata);
    }

    [Fact]
    public void ValidateTtl_Negative_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateTtl(-1));
    }

    [Fact]
    public void ValidateTtl_Zero_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateTtl(0));
    }

    [Fact]
    public void ValidateTtl_TooLarge_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateTtl(2592001));
    }

    [Fact]
    public void ValidateTtl_Valid_Returns()
    {
        StorageValidationHelper.ValidateTtl(86400);
    }

    [Fact]
    public void ValidateTtl_MaxValue_Returns()
    {
        StorageValidationHelper.ValidateTtl(2592000);
    }

    [Fact]
    public void ValidateDirectoryPath_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateDirectoryPath(""));
    }

    [Fact]
    public void ValidateDirectoryPath_Traversal_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateDirectoryPath("../etc"));
    }

    [Fact]
    public void ValidateDirectoryPath_DriveLetter_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateDirectoryPath("C:folder"));
    }

    [Fact]
    public void ValidateDirectoryPath_TooLong_Throws()
    {
        var longPath = new string('a', 4097);
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateDirectoryPath(longPath));
    }

    [Fact]
    public void ValidateDirectoryPath_Valid_Returns()
    {
        StorageValidationHelper.ValidateDirectoryPath("docs/projects");
    }

    [Fact]
    public void ValidateDirectoryName_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateDirectoryName(""));
    }

    [Fact]
    public void ValidateDirectoryName_ContainsSlash_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateDirectoryName("dir/name"));
    }

    [Fact]
    public void ValidateDirectoryName_ContainsBackslash_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageValidationHelper.ValidateDirectoryName("dir\\name"));
    }

    [Fact]
    public void ValidateDirectoryName_Valid_Returns()
    {
        StorageValidationHelper.ValidateDirectoryName("valid_dir");
    }
}
