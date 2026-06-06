using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Engine.Interfaces;

namespace Client.Engine.Services
{
    public class BridgeTaskQueue : ITaskQueue
    {
        private readonly ILogger<BridgeTaskQueue> _logger;
        public BridgeTaskQueue(ILogger<BridgeTaskQueue> logger)
        {
            _logger = logger;
        }
    }
}