using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Server.Models
{
    [Table("groups", Schema = "identity")]
    public class Group
    {
        [Key]
        public Guid Id { get; set; }
        public required Guid CompanyId { get; set; }
        public Guid? IconId { get; set; }
        public Icon? Icon { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
        public Company? Company { get; set; }
    }

    [Table("group_members", Schema = "identity")]
    public class GroupMember
    {
        public required Guid GroupId { get; set; }
        public required Guid UserId { get; set; }
        public required Guid CompanyId { get; set; }
        public required DateTime JoinDate { get; set; }
        public required GroupRole Role { get; set; }
    }

    public enum GroupRole
    {
        /// <summary>
        /// Read-only
        /// </summary>
        Guest,
        /// <summary>
        /// Read-write
        /// </summary>
        User, 
        /// <summary>
        /// Read-write and can add/remove users
        /// </summary>
        Admin, 
        /// <summary>
        /// Read-write and can add/remove users and change settings
        /// </summary>
        Owner 
    }

}