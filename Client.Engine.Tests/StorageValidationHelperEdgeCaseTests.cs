using Client.Engine.Helpers;
using FluentAssertions;

namespace Client.Engine.Tests;

public class StorageValidationHelperEdgeCaseTests
{
    // ── ValidateFileName edge cases ────────────────────────────────────────

    [Fact]
    public void ValidateFileName_BackslashAtPositionZero_Throws()
    {
        var act = () => StorageValidationHelper.ValidateFileName("\\filename.txt");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*разделителей*");
    }

    [Fact]
    public void ValidateFileName_NullInput_Throws()
    {
        var act = () => StorageValidationHelper.ValidateFileName(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFileName_ExactlyMaxLength_ReturnsSuccessfully()
    {
        // 1024 characters is the boundary - should be accepted
        var name = new string('a', 1024);

        var act = () => StorageValidationHelper.ValidateFileName(name);

        act.Should().NotThrow();
    }

    // ── ValidateMetadata edge cases ────────────────────────────────────────

    [Fact]
    public void ValidateMetadata_WhitespaceOnlyKey_Throws()
    {
        var metadata = new Dictionary<string, string> { { "   ", "value" } };

        var act = () => StorageValidationHelper.ValidateMetadata(metadata);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*пустым*");
    }

    [Fact]
    public void ValidateMetadata_EmptyValue_ReturnsSuccessfully()
    {
        // Empty values should be allowed (only keys are validated for emptiness)
        var metadata = new Dictionary<string, string> { { "key", "" } };

        var act = () => StorageValidationHelper.ValidateMetadata(metadata);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateMetadata_Exactly64Keys_ReturnsSuccessfully()
    {
        var metadata = new Dictionary<string, string>();
        for (int i = 0; i < 64; i++)
        {
            metadata[$"key{i}"] = "value";
        }

        var act = () => StorageValidationHelper.ValidateMetadata(metadata);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateMetadata_ReservedKeyCaseInsensitive_Throws()
    {
        // storageKey, ownerId, physicalPath, bucketName are reserved (case-insensitive)
        var metadata = new Dictionary<string, string> { { "STORAGEKEY", "value" } };

        var act = () => StorageValidationHelper.ValidateMetadata(metadata);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*зарезервирован*");
    }

    // ── ValidatePath edge cases ────────────────────────────────────────────

    [Fact]
    public void ValidatePath_ExactlyRootSlash_Throws()
    {
        // "/" starts with '/', which is rejected as an absolute path
        var act = () => StorageValidationHelper.ValidatePath("/");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*относительным*");
    }

    [Fact]
    public void ValidatePath_NullInput_Throws()
    {
        var act = () => StorageValidationHelper.ValidatePath(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidatePath_WhitespaceInput_Throws()
    {
        var act = () => StorageValidationHelper.ValidatePath("   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*пустым*");
    }

    [Fact]
    public void ValidatePath_ExactlyMaxLength_ReturnsSuccessfully()
    {
        var path = new string('a', 4096);

        var act = () => StorageValidationHelper.ValidatePath(path);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidatePath_PathWithEmbeddedTraversal_Throws()
    {
        // ".." embedded in the middle, not just at start
        var act = () => StorageValidationHelper.ValidatePath("docs/../etc");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Path traversal*");
    }

    // ── ValidateDirectoryName edge cases ───────────────────────────────────

    [Fact]
    public void ValidateDirectoryName_VeryLongName_DoesNotThrow()
    {
        // ValidateDirectoryName only checks for whitespace and path separators,
        // not length. Long names are accepted here.
        var longName = new string('d', 256);

        var act = () => StorageValidationHelper.ValidateDirectoryName(longName);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateDirectoryName_NullInput_Throws()
    {
        var act = () => StorageValidationHelper.ValidateDirectoryName(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateDirectoryName_WhitespaceInput_Throws()
    {
        var act = () => StorageValidationHelper.ValidateDirectoryName("   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*пустым*");
    }

    // ── ValidateTtl edge cases ─────────────────────────────────────────────

    [Fact]
    public void ValidateTtl_NegativeValue_Throws()
    {
        var act = () => StorageValidationHelper.ValidateTtl(-100);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("ttlSeconds");
    }

    [Fact]
    public void ValidateTtl_ExactlyMaxValue_ReturnsSuccessfully()
    {
        var act = () => StorageValidationHelper.ValidateTtl(2592000);

        act.Should().NotThrow();
    }

    // ── ValidateDirectoryPath edge cases ───────────────────────────────────

    [Fact]
    public void ValidateDirectoryPath_NullInput_Throws()
    {
        var act = () => StorageValidationHelper.ValidateDirectoryPath(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateDirectoryPath_WhitespaceInput_Throws()
    {
        var act = () => StorageValidationHelper.ValidateDirectoryPath("   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*пустым*");
    }

    [Fact]
    public void ValidateDirectoryPath_ExactlyMaxLength_ReturnsSuccessfully()
    {
        var path = new string('d', 4096);

        var act = () => StorageValidationHelper.ValidateDirectoryPath(path);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateDirectoryPath_PathWithDriveLetter_Throws()
    {
        var act = () => StorageValidationHelper.ValidateDirectoryPath("C:Windows");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*буквы дисков*");
    }

    [Fact]
    public void ValidateDirectoryPath_EmbeddedTraversal_Throws()
    {
        var act = () => StorageValidationHelper.ValidateDirectoryPath("docs/../etc");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Path traversal*");
    }
}