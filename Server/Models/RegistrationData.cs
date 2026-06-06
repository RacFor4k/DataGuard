using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Models
{
    public class RegistrationData
    {
        public required Guid CompanyId { get; set; }
        public required string Name { get; set; }
        public required string Surname { get; set; }
        public required string Email { get; set; }
        public required IEnumerable<string> Groups { get; set; }
        public required IEnumerable<Guid> AdminGroups { get; set; }
        public required string PinCodeHash { get; set; }
        public required byte[] EncryptedKey { get; set; }
        public required byte[] MasterPublicKey { get; set; }
    }
}