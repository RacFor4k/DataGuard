using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Server.Storage.Services;

namespace Server.Storage.Tests;

public class JwtOwnerIdentityProviderTests
{
    private readonly JwtOwnerIdentityProvider _provider = new();

    [Fact]
    public void GetOwnerId_ValidSubClaim_ReturnsGuid()
    {
        var expectedId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("sub", expectedId.ToString()) }
            )
        );

        var result = _provider.GetOwnerId(httpContext);

        result.Should().NotBeNull();
        result.Should().Be(expectedId);
    }

    [Fact]
    public void GetOwnerId_InvalidSubClaim_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("sub", "not-a-guid") }
            )
        );

        var result = _provider.GetOwnerId(httpContext);

        result.Should().BeNull();
    }

    [Fact]
    public void GetOwnerId_MissingSubClaim_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("name", "John") }
            )
        );

        var result = _provider.GetOwnerId(httpContext);

        result.Should().BeNull();
    }

    [Fact]
    public void GetOwnerId_NoClaimsPrincipal_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(); // empty — no identity

        var result = _provider.GetOwnerId(httpContext);

        result.Should().BeNull();
    }

    [Fact]
    public void GetOwnerId_NullUser_ThrowsNullReferenceException()
    {
        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(c => c.User).Returns((System.Security.Claims.ClaimsPrincipal?)null);

        var act = () => _provider.GetOwnerId(httpContextMock.Object);

        act.Should().Throw<NullReferenceException>();
    }
}