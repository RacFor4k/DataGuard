using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Auth.Options
{
    public class CompanyManagerOptions
    {
        [Required(ErrorMessage = "Company:MasterKeyHash is empty")]
        public required byte[] MasterKeyHash { get; set; }
    }
}