using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Auth.Interfaces
{
    public interface ISecurityService
    {
        public Task<string> GetNonceToken();
        public Task<bool> VerifyNonceToken(string nonce);
        public byte[] GenerateSalt();
        public Task<byte[]> HashPasswordAsync(string password, byte[] salt);
        public Task<byte[]> HashPasswordAsync(byte[] password, byte[] salt);
    }
}