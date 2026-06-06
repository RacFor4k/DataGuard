using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Engine.Interfaces;

namespace Client.Engine.Services
{
    public class TaskReceiver : ITaskReceiver
    {
        private readonly ILogger<TaskReceiver> _logger;
        public TaskReceiver(ILogger<TaskReceiver> logger)
        {
            _logger = logger;
        }
    }
}