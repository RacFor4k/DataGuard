using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Server.Auth.Interfaces;
using Server.Auth.Middlewares;
using Server.Auth.Services;

namespace Server.Auth.Tests;

public class JwtMiddlewareTests
{
    private static JwtMiddleware CreateMiddleware(RequestDelegate next) => new(next);

    private static DefaultHttpContext CreateHttpContext(string? authHeader = null)
    {
        var context = new DefaultHttpContext();
        if (authHeader != null)
        {
            context.Request.Headers["Authorization"] = authHeader;
        }
        return context;
    }

    [Fact]
    public async Task InvokeAsync_NoAuthHeader_UserJwtStaysNull()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var jwtService = new Mock<IJwtService>();
        var userAccessor = new UserAccessor();
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context, jwtService.Object, userAccessor);

        nextCalled.Should().BeTrue("next delegate should be called");
        userAccessor.UserJwt.Should().BeNull();
        jwtService.Verify(j => j.VerifyTokenAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_NoBearerPrefix_UserJwtStaysNull()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var jwtService = new Mock<IJwtService>();
        var userAccessor = new UserAccessor();
        var context = CreateHttpContext("Basic dXNlcjpwYXNz");

        await middleware.InvokeAsync(context, jwtService.Object, userAccessor);

        nextCalled.Should().BeTrue();
        userAccessor.UserJwt.Should().BeNull();
        jwtService.Verify(j => j.VerifyTokenAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_SetsUserJwt()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var expectedToken = new JwtSecurityToken(
            claims: [new Claim(JwtRegisteredClaimNames.Typ, "access")]);
        var jwtService = new Mock<IJwtService>();
        jwtService.Setup(j => j.VerifyTokenAsync("valid-token")).ReturnsAsync(expectedToken);

        var userAccessor = new UserAccessor();
        var context = CreateHttpContext("Bearer valid-token");

        await middleware.InvokeAsync(context, jwtService.Object, userAccessor);

        nextCalled.Should().BeTrue();
        userAccessor.UserJwt.Should().BeSameAs(expectedToken);
    }

    [Fact]
    public async Task InvokeAsync_InvalidToken_UserJwtStaysNull()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var jwtService = new Mock<IJwtService>();
        jwtService.Setup(j => j.VerifyTokenAsync("invalid-token")).ReturnsAsync((JwtSecurityToken?)null);

        var userAccessor = new UserAccessor();
        var context = CreateHttpContext("Bearer invalid-token");

        await middleware.InvokeAsync(context, jwtService.Object, userAccessor);

        nextCalled.Should().BeTrue();
        userAccessor.UserJwt.Should().BeNull();
    }
}