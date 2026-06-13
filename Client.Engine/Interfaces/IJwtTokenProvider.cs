using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Engine.Models;

namespace Client.Engine.Interfaces
{
    public interface IJwtTokenProvider
    {
        void SetToken(JwtToken token);
        Task<string> GetOrRefreshTokenAsync();
    }
}