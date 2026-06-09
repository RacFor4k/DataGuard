using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Models
{
    [Table("companies", Schema = "identity")]
    public class Company
    {
        [Key]
        public Guid CompanyId { get; set; } = Guid.CreateVersion7();
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public byte[]? PublicKey { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Group> Groups { get; set; } = new List<Group>();
    }
}