using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Options
{
    public class SecurityOptions
    {
        public byte[]? NonceSecretKey { get; set; }
        public int SaltLength { get; set; } = 32; // 256 bit
        public int PasswordHashLength { get; set; } = 32; // 256 bit
        public Argon2Options Argon2 { get; set; } = new();
        public class Argon2Options
        {
            public int DegreeOfParallelism { get; set; } = 1;
            public int Iterations { get; set; } = 3;
            public int MemorySize { get; set; } = 19456; // 19 MB
        }
    }
}