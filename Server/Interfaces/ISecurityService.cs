using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Interfaces
{
    public interface ISecurityService
    {
        public Task<string> GetNonceToken();
        public Task<bool> VerifyNonceToken(string nonce);
    }
}