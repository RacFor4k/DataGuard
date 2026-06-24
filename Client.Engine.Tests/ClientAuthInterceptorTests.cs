using System.Threading.Tasks;
using Client.Engine.Interceptors;
using FluentAssertions;
using Grpc.Core;
using Moq;

namespace Client.Engine.Tests;

public class ClientAuthInterceptorTests
{
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<ClientAuthInterceptor>> _loggerMock;

    public ClientAuthInterceptorTests()
    {
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ClientAuthInterceptor>>();
    }

    /// <summary>
    /// Concrete ServerCallContext subclass for testing.
    /// Required because ServerCallContext.RequestHeaders (and many other members)
    /// are non-virtual, so Moq cannot intercept them.
    /// </summary>
    private class TestServerCallContext : ServerCallContext
    {
        private readonly Metadata _headers;
        private readonly Metadata _responseTrailers;
        private readonly AuthContext _authContext;
        private Status _status = Status.DefaultSuccess;
        private WriteOptions? _writeOptions;

        public TestServerCallContext(Metadata? requestHeaders = null)
        {
            _headers = requestHeaders ?? new Metadata();
            _responseTrailers = new Metadata();
            _authContext = new AuthContext(string.Empty, new Dictionary<string, List<AuthProperty>>());
        }

        protected override Metadata RequestHeadersCore => _headers;

        protected override Metadata ResponseTrailersCore => _responseTrailers;

        protected override AuthContext AuthContextCore => _authContext;

        protected override string MethodCore => "TestService/TestMethod";

        protected override string HostCore => "localhost";

        protected override string PeerCore => "ipv4:127.0.0.1:1234";

        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);

        protected override CancellationToken CancellationTokenCore => CancellationToken.None;

        protected override Status StatusCore { get => _status; set => _status = value; }

        protected override WriteOptions? WriteOptionsCore { get => _writeOptions; set => _writeOptions = value; }

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => null!;

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            => Task.CompletedTask;
    }

    private static TestServerCallContext CreateTestContext(string? token = null)
    {
        var headers = new Metadata();
        if (token != null)
        {
            headers.Add("x-client-token", token);
        }
        return new TestServerCallContext(headers);
    }

    // ── AllowToken / ValidateClientToken core behaviour ────────────────────

    [Fact]
    public void AllowToken_ThenValidate_ValidToken_DoesNotThrow()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);
        interceptor.AllowToken("good-token");

        var context = CreateTestContext("good-token");

        // UnaryServerHandler should not throw, and continuation should be invoked
        var continuationCalled = false;
        UnaryServerMethod<string, string> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            return Task.FromResult("ok");
        };

        var task = interceptor.UnaryServerHandler("req", context, continuation);

        task.IsCompletedSuccessfully.Should().BeTrue();
        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AllowToken_ThenValidate_DifferentToken_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);
        interceptor.AllowToken("allowed-token");

        var context = CreateTestContext("wrong-token");

        var continuationCalled = false;
        UnaryServerMethod<string, string> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            return Task.FromResult("ok");
        };

        var act = () => interceptor.UnaryServerHandler("req", context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    // ── UnaryServerHandler ─────────────────────────────────────────────────

    [Fact]
    public async Task UnaryServerHandler_NoClientTokenHeader_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);

        var context = CreateTestContext(token: null);
        var continuationCalled = false;
        UnaryServerMethod<string, string> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            return Task.FromResult("ok");
        };

        var act = () => interceptor.UnaryServerHandler("req", context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    [Fact]
    public async Task UnaryServerHandler_UnknownToken_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);

        var context = CreateTestContext("unknown-token");
        var continuationCalled = false;
        UnaryServerMethod<string, string> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            return Task.FromResult("ok");
        };

        var act = () => interceptor.UnaryServerHandler("req", context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    [Fact]
    public async Task UnaryServerHandler_ValidToken_CallsContinuation()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);
        interceptor.AllowToken("valid-token");

        var context = CreateTestContext("valid-token");
        var continuationCalled = false;
        UnaryServerMethod<string, string> continuation = (req, ctx) =>
        {
            continuationCalled = true;
            return Task.FromResult("result");
        };

        var result = await interceptor.UnaryServerHandler("req", context, continuation);

        continuationCalled.Should().BeTrue();
        result.Should().Be("result");
    }

    // ── ClientStreamingServerHandler ───────────────────────────────────────

    [Fact]
    public async Task ClientStreamingServerHandler_NoClientTokenHeader_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);

        var context = CreateTestContext(token: null);
        var continuationCalled = false;
        var requestStream = new Mock<IAsyncStreamReader<string>>().Object;
        ClientStreamingServerMethod<string, string> continuation = (stream, ctx) =>
        {
            continuationCalled = true;
            return Task.FromResult("ok");
        };

        var act = () => interceptor.ClientStreamingServerHandler(requestStream, context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ClientStreamingServerHandler_UnknownToken_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);

        var context = CreateTestContext("unknown-token");
        var continuationCalled = false;
        var requestStream = new Mock<IAsyncStreamReader<string>>().Object;
        ClientStreamingServerMethod<string, string> continuation = (stream, ctx) =>
        {
            continuationCalled = true;
            return Task.FromResult("ok");
        };

        var act = () => interceptor.ClientStreamingServerHandler(requestStream, context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ClientStreamingServerHandler_ValidToken_CallsContinuation()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);
        interceptor.AllowToken("valid-token");

        var context = CreateTestContext("valid-token");
        var continuationCalled = false;
        var requestStream = new Mock<IAsyncStreamReader<string>>().Object;
        ClientStreamingServerMethod<string, string> continuation = (stream, ctx) =>
        {
            continuationCalled = true;
            return Task.FromResult("result");
        };

        var result = await interceptor.ClientStreamingServerHandler(requestStream, context, continuation);

        continuationCalled.Should().BeTrue();
        result.Should().Be("result");
    }

    // ── ServerStreamingServerHandler ───────────────────────────────────────

    [Fact]
    public async Task ServerStreamingServerHandler_NoClientTokenHeader_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);

        var context = CreateTestContext(token: null);
        var continuationCalled = false;
        var responseStream = new Mock<IServerStreamWriter<string>>().Object;
        ServerStreamingServerMethod<string, string> continuation = (req, stream, ctx) =>
        {
            continuationCalled = true;
            return Task.CompletedTask;
        };

        var act = () => interceptor.ServerStreamingServerHandler("req", responseStream, context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ServerStreamingServerHandler_UnknownToken_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);

        var context = CreateTestContext("unknown-token");
        var continuationCalled = false;
        var responseStream = new Mock<IServerStreamWriter<string>>().Object;
        ServerStreamingServerMethod<string, string> continuation = (req, stream, ctx) =>
        {
            continuationCalled = true;
            return Task.CompletedTask;
        };

        var act = () => interceptor.ServerStreamingServerHandler("req", responseStream, context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ServerStreamingServerHandler_ValidToken_CallsContinuation()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);
        interceptor.AllowToken("valid-token");

        var context = CreateTestContext("valid-token");
        var continuationCalled = false;
        var responseStream = new Mock<IServerStreamWriter<string>>().Object;
        ServerStreamingServerMethod<string, string> continuation = (req, stream, ctx) =>
        {
            continuationCalled = true;
            return Task.CompletedTask;
        };

        await interceptor.ServerStreamingServerHandler("req", responseStream, context, continuation);

        continuationCalled.Should().BeTrue();
    }

    // ── DuplexStreamingServerHandler ───────────────────────────────────────

    [Fact]
    public async Task DuplexStreamingServerHandler_NoClientTokenHeader_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);

        var context = CreateTestContext(token: null);
        var continuationCalled = false;
        var requestStream = new Mock<IAsyncStreamReader<string>>().Object;
        var responseStream = new Mock<IServerStreamWriter<string>>().Object;
        DuplexStreamingServerMethod<string, string> continuation = (reqStream, respStream, ctx) =>
        {
            continuationCalled = true;
            return Task.CompletedTask;
        };

        var act = () => interceptor.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DuplexStreamingServerHandler_UnknownToken_ThrowsUnauthenticated()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);

        var context = CreateTestContext("unknown-token");
        var continuationCalled = false;
        var requestStream = new Mock<IAsyncStreamReader<string>>().Object;
        var responseStream = new Mock<IServerStreamWriter<string>>().Object;
        DuplexStreamingServerMethod<string, string> continuation = (reqStream, respStream, ctx) =>
        {
            continuationCalled = true;
            return Task.CompletedTask;
        };

        var act = () => interceptor.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Unauthenticated);
        continuationCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DuplexStreamingServerHandler_ValidToken_CallsContinuation()
    {
        var interceptor = new ClientAuthInterceptor(_loggerMock.Object);
        interceptor.AllowToken("valid-token");

        var context = CreateTestContext("valid-token");
        var continuationCalled = false;
        var requestStream = new Mock<IAsyncStreamReader<string>>().Object;
        var responseStream = new Mock<IServerStreamWriter<string>>().Object;
        DuplexStreamingServerMethod<string, string> continuation = (reqStream, respStream, ctx) =>
        {
            continuationCalled = true;
            return Task.CompletedTask;
        };

        await interceptor.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);

        continuationCalled.Should().BeTrue();
    }
}