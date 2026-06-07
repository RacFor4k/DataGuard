using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Server.Models
{
    [Table("users", Schema = "identity")]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        public Guid UUID { get; set; } = Guid.CreateVersion7();
        public required Guid CompanyId { get; set; }
        public required string Name { get; set; }
        public required string Surname { get; set; }
        public required string Email { get; set; }
        public ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
        public required string PinCodeHash { get; set; }
        public required byte[] PublicKey { get; set; }
        public required byte[] EncryptedKey { get; set; }
        public byte[]? MasterEncryptedKey { get; set; }
        public required Company Company { get; set; }
    }


}