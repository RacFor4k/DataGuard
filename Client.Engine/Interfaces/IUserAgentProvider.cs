using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Engine.Interfaces
{
    public interface IUserAgentProvider
    {
        string GetUserAgent();
    }
}