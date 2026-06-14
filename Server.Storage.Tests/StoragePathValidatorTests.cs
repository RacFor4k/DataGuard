using Server.Storage.Services;

namespace Server.Storage.Tests;

public class StoragePathValidatorTests
{
    private readonly StoragePathValidator _validator = new();

    [Theory]
    [InlineData("docs/report.txt", "/docs/report.txt")]
    [InlineData("\\docs\\report.txt", "/docs/report.txt")]
    [InlineData("///docs/projects///report.txt", "/docs/projects/report.txt")]
    public void TryNormalizePath_ValidInput_NormalizesSuccessfully(string rawPath, string expectedNormalized)
    {
        bool success = _validator.TryNormalizePath(rawPath, out string normalized, out string? error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(expectedNormalized, normalized);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("docs/../../secret")]
    [InlineData("C:\\Windows\\system32")]
    [InlineData("")]
    public void TryNormalizePath_InvalidOrMaliciousInput_FailsValidation(string rawPath)
    {
        bool success = _validator.TryNormalizePath(rawPath, out string _, out string? error);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public void IsPathInside_ChildInsideParent_ReturnsTrue()
    {
        Assert.True(_validator.IsPathInside("/docs", "/docs/projects/report.txt"));
    }

    [Fact]
    public void IsPathInside_ChildOutsideParent_ReturnsFalse()
    {
        Assert.False(_validator.IsPathInside("/docs", "/other/report.txt"));
    }

    [Fact]
    public void GetFileName_ValidPath_ReturnsFileName()
    {
        Assert.Equal("report.txt", _validator.GetFileName("/docs/report.txt"));
    }

    [Fact]
    public void GetParentPath_ValidPath_ReturnsParent()
    {
        Assert.Equal("/docs", _validator.GetParentPath("/docs/report.txt"));
    }

    [Fact]
    public void Combine_ParentAndChild_ReturnsCombined()
    {
        Assert.Equal("/docs/projects", _validator.Combine("/docs", "projects"));
    }
}
