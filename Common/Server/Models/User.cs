using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Common.Server.Models
{
    [Table("users", Schema = "identity")]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        public Guid UserId { get; set; } = Guid.CreateVersion7();
        public required string Name { get; set; }
        public required string Surname { get; set; }
        public required string Email { get; set; }
        public ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
        public required byte[] EncryptedPassword { get; set; }
        public required byte[] EncryptedKey { get; set; }
        public required byte[] ServerPasswordHash { get; set; }
        public required byte[] ClientSalt { get; set; }
        public required byte[] ServerSalt { get; set; }
        public byte[]? BackupEncryptedKey { get; set; }
        public required Guid CompanyId { get; set; }
        public required Company Company { get; set; }
    }


}