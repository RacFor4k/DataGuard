using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Engine.Options
{
    public class SecurityOptions
    {
        public int SaltLength { get; set; } = 32; // 256 bit
        public int HashLength { get; set; } = 32; // 256 bit
        public int HashIterations { get; set; } = 600000;
        public int NonceLength { get; set; } = 12; // 96 bit
        public int TagLength { get; set; } = 16; // 128 bit
        [Required(ErrorMessage = "Master key salt is empty")]
        public required byte[] MasterKeySalt { get; set; }
        public Argon2Options Argon2 { get; set; } = new();
        public class Argon2Options
        {
            public int DegreeOfParallelism { get; set; } = 1;
            public int Iterations { get; set; } = 3;
            public int MemorySize { get; set; } = 19456; // 19 MB
        }
        public PasswordOptions Password { get; set; } = new();
        public class PasswordOptions
        {
            public int MinimumLength { get; set; } = 8;
            public int MaximumLength { get; set; } = 16;
        }


    }
}