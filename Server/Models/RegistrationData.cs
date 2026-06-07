using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Models
{
    public class RegistrationData
    {
        public Guid CompanyId { get; set; } = Guid.CreateVersion7();
        public required string Name { get; set; }
        public required string Surname { get; set; }
        public required string Email { get; set; }
        public required IEnumerable<Guid> Groups { get; set; }
        public required IEnumerable<Guid> AdminGroups { get; set; }
    }
}