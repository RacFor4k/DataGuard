using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Engine.Workers
{
    public class QueueProcessorWorker : BackgroundService
    {
        private readonly ILogger<QueueProcessorWorker> _logger;
        public QueueProcessorWorker(ILogger<QueueProcessorWorker> logger)
        {
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            
        }
    }
}