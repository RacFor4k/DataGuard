using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models.Db.Identity
{
    public enum Role
    {
        Guest = 0,
        User = 1,
        Admin = 1<<1,
    }
    public class UserGroup
    {
        public int UserId { get; set; }
        public User User { get; set; }
        public int GroupId { get; set; }
        public Group Group { get; set; }

        [Column(TypeName = "intager")]
        public Role Role { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
