using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.Protos.Auth;

namespace Client.Engine.Workers
{
    public class QueueProcessorWorker : BackgroundService
    {
        private readonly ILogger<QueueProcessorWorker> _logger;
        private readonly Authentication.AuthenticationClient _authClient;
        public QueueProcessorWorker(Authentication.AuthenticationClient authClient, ILogger<QueueProcessorWorker> logger)
        {
            _authClient = authClient;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            
        }
    }
}