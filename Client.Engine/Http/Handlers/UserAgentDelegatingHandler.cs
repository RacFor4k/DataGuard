using Client.Engine.Interfaces;

namespace Client.Engine.Http.Handlers
{
    public class UserAgentDelegatingHandler : DelegatingHandler
    {
        private readonly IUserAgentProvider _userAgentProvider;
        private readonly ILogger<UserAgentDelegatingHandler> _logger;
        public UserAgentDelegatingHandler(IUserAgentProvider userAgentProvider, ILogger<UserAgentDelegatingHandler> logger)
        {
            _userAgentProvider = userAgentProvider;
            _logger = logger;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.UserAgent.Clear();
            _logger.LogTrace($"User-Agent: {_userAgentProvider.GetUserAgent()}");
            request.Headers.UserAgent.ParseAdd(_userAgentProvider.GetUserAgent());
            return await base.SendAsync(request, cancellationToken);
        }
    }
}