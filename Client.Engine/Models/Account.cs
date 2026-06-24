using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Engine.Models
{
    [Table("accounts")]
    public class Account
    {
        public required Guid AccountId { get; set; }
        public required string Email { get; set; }
        public JwtToken? JwtToken { get; set; }
    }
}