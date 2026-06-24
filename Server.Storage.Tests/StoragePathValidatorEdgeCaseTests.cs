using FluentAssertions;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StoragePathValidatorEdgeCaseTests
{
    private readonly StoragePathValidator _validator = new();

    // ── TryNormalizePath edge cases ──────────────────────────────────────────

    [Fact]
    public void TryNormalizePath_PathExceeds4096Chars_ReturnsFalse()
    {
        string longSegment = new('a', 4097);
        bool ok = _validator.TryNormalizePath(longSegment, out _, out string? error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void TryNormalizePath_PathDepthExceeds64_ReturnsFalse()
    {
        var segments = Enumerable.Range(0, 65).Select(i => $"dir{i}");
        string deepPath = string.Join("/", segments);

        bool ok = _validator.TryNormalizePath(deepPath, out _, out string? error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void TryNormalizePath_DoubleDotSegments_ReturnsFalse()
    {
        bool ok = _validator.TryNormalizePath("docs/../etc/passwd", out _, out string? error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void TryNormalizePath_SingleDotSegments_ReturnsFalse()
    {
        bool ok = _validator.TryNormalizePath("docs/./file.txt", out _, out string? error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void TryNormalizePath_WhitespaceOnlySegment_ReturnsFalse()
    {
        bool ok = _validator.TryNormalizePath("docs/  /file.txt", out _, out string? error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void TryNormalizePath_RootLevelFileName_ReturnsSlashPrefixed()
    {
        bool ok = _validator.TryNormalizePath("file.txt", out string normalized, out string? error);

        ok.Should().BeTrue();
        normalized.Should().Be("/file.txt");
        error.Should().BeNull();
    }

    // ── IsPathInside edge cases ──────────────────────────────────────────────

    [Fact]
    public void IsPathInside_EqualPaths_ReturnsTrue()
    {
        _validator.IsPathInside("/docs", "/docs").Should().BeTrue();
    }

    [Fact]
    public void IsPathInside_ParentWithTrailingSlash_ReturnsTrue()
    {
        _validator.IsPathInside("/docs/", "/docs/file.txt").Should().BeTrue();
    }

    [Fact]
    public void IsPathInside_ChildWithTrailingSlash_ReturnsTrue()
    {
        _validator.IsPathInside("/docs", "/docs/sub/").Should().BeTrue();
    }

    [Fact]
    public void IsPathInside_BothWithTrailingSlash_ReturnsTrue()
    {
        _validator.IsPathInside("/docs/", "/docs/sub/").Should().BeTrue();
    }

    [Fact]
    public void IsPathInside_SimilarPrefixNotChild_ReturnsFalse()
    {
        _validator.IsPathInside("/docs", "/docs2/file.txt").Should().BeFalse();
    }

    // ── GetFileName edge cases ───────────────────────────────────────────────

    [Fact]
    public void GetFileName_EmptyString_ReturnsEmpty()
    {
        _validator.GetFileName("").Should().BeEmpty();
    }

    [Fact]
    public void GetFileName_RootPath_ReturnsEmpty()
    {
        _validator.GetFileName("/").Should().BeEmpty();
    }

    [Fact]
    public void GetFileName_NoSlash_ReturnsSameString()
    {
        _validator.GetFileName("file.txt").Should().Be("file.txt");
    }

    // ── GetParentPath edge cases ─────────────────────────────────────────────

    [Fact]
    public void GetParentPath_RootPath_ReturnsRoot()
    {
        _validator.GetParentPath("/").Should().Be("/");
    }

    [Fact]
    public void GetParentPath_EmptyString_ReturnsRoot()
    {
        _validator.GetParentPath("").Should().Be("/");
    }

    [Fact]
    public void GetParentPath_SingleSegment_ReturnsRoot()
    {
        _validator.GetParentPath("/file.txt").Should().Be("/");
    }

    [Fact]
    public void GetParentPath_TrailingSlash_StripsSlash()
    {
        _validator.GetParentPath("/docs/projects/").Should().Be("/docs");
    }

    // ── Combine edge cases ───────────────────────────────────────────────────

    [Fact]
    public void Combine_RootParent_ReturnsSlashChild()
    {
        _validator.Combine("/", "file.txt").Should().Be("/file.txt");
    }

    [Fact]
    public void Combine_RootParentWithTrailingSlash_ReturnsSlashChild()
    {
        _validator.Combine("/", "file.txt").Should().Be("/file.txt");
    }

    [Fact]
    public void Combine_ChildWithLeadingSlash_StripsSlash()
    {
        _validator.Combine("/docs", "/file.txt").Should().Be("/docs/file.txt");
    }
}