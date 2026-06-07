using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Models
{
    [Table("icons", Schema = "identity")]
    public class Icon
    {
        [Key]
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid? CompanyId { get; set; }
        public required string Path { get; set; }
        public string? Name { get; set; }

    }
}