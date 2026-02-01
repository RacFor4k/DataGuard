using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models.Db.Identity
{
    [Flags]
    public enum SecurityPermissions
    {
        None = 0,
        CanShare = 1,
        AllAccess = int.MaxValue,
    }
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [Column(TypeName = "integer")]
        public SecurityPermissions SecurityPermissions { get; set; }
        public int CompanyId { get; set; }
        public Company Company { get; set; }

    }
}
