using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Client.Engine.Models;
using Client.Engine.Services;
using Contracts.Protos.Auth;

namespace Client.Engine.Workers
{
    public class QueueProcessorWorker : BackgroundService
    {
        private readonly ILogger<QueueProcessorWorker> _logger;
        private readonly Authentication.AuthenticationClient _authClient;
        private readonly ChannelReader<BrigeTask> _taskReader;
        public QueueProcessorWorker(Authentication.AuthenticationClient authClient, Channel<BrigeTask> taskReader, ILogger<QueueProcessorWorker> logger)
        {
            _authClient = authClient;
            _taskReader = taskReader;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("QueueProcessorWorker started");
            try
            {
                while (await _taskReader.WaitToReadAsync(cancellationToken))
                {
                    while(_taskReader.TryRead(out BrigeTask? task))
                    {
                        try
                        {
                            await ProcessTaskAsync(task);
                        }
                        catch (Exception e)
                        {
                            
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("QueueProcessorWorker stopped (cancellation)");
            }
        }

        private async Task ProcessTaskAsync(BrigeTask task)
        {
            
        }
    }
}