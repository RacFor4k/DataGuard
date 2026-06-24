using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Client.Engine.Interceptors
{
    /// <summary>
    /// gRPC-перехватчик для проверки токена клиента.
    /// Каждый GUI-клиент должен передавать заголовок "x-client-token" с уникальным GUID.
    /// </summary>
    public class ClientAuthInterceptor : Interceptor
    {
        private readonly ConcurrentDictionary<string, byte> _allowedTokens = new();
        private readonly ILogger<ClientAuthInterceptor> _logger;

        public ClientAuthInterceptor(ILogger<ClientAuthInterceptor> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Добавить токен в список разрешённых. Вызывается при старте GUI-клиента.
        /// </summary>
        public void AllowToken(string token)
        {
            _allowedTokens.TryAdd(token, 0);
            _logger.LogDebug("Токен клиента добавлен в разрешённые");
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            ValidateClientToken(context);
            return await continuation(request, context);
        }

        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream,
            ServerCallContext context,
            ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            ValidateClientToken(context);
            return await continuation(requestStream, context);
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
            TRequest request,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context,
            ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            ValidateClientToken(context);
            await continuation(request, responseStream, context);
        }

        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context,
            DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            ValidateClientToken(context);
            await continuation(requestStream, responseStream, context);
        }

        private void ValidateClientToken(ServerCallContext context)
        {
            // Пропускаем проверку для локальных консольных вызовов (без заголовка)
            var tokenEntry = context.RequestHeaders.FirstOrDefault(e => e.Key == "x-client-token");
            if (tokenEntry == null)
            {
                _logger.LogWarning("Отклонён вызов без токена клиента: {Method}", context.Method);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Отсутствует токен клиента (x-client-token)."));
            }

            var token = tokenEntry.Value;
            if (!_allowedTokens.ContainsKey(token))
            {
                _logger.LogWarning("Отклонён вызов с неизвестным токеном клиента: {Method}", context.Method);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Неверный токен клиента."));
            }
        }
    }
}