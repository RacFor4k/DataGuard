using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Interfaces
{
    public interface INonceSecureRequest
    {
        string Nonce { get; }
        string ClientSignature { get; }
        string ServerSignature { get; }
    }
}