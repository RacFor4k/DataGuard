using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Auth.Options
{
    public class SecurityOptions
    {
        [Required(ErrorMessage = "Nonce secret key is required")]
        public required byte[] NonceSecretKey { get; set; }
        public int SaltLength { get; set; } = 32; // 256 bit
        public int PasswordHashLength { get; set; } = 32; // 256 bit
        public int EncryptedPasswordLength { get; set; } = 64; // 512 bit
        public int EncryptedKeyLength { get; set; } = 32; // 256 bit
        public int NonceLength { get; set; } = 12; 
        public int TagLength { get; set; } = 16;
        [Required(ErrorMessage = "Master key salt is empty")]
        public required byte[] MasterKeySalt { get; set; }
        public Argon2Options Argon2 { get; set; } = new();
        public class Argon2Options
        {
            public int DegreeOfParallelism { get; set; } = 1;
            public int Iterations { get; set; } = 3;
            public int MemorySize { get; set; } = 19456; // 19 MB
        }
        public int RsaKeySize { get; set; } = 4096;
    }
}