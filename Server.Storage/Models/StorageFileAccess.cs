using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Storage.Models;

[Table("storage_file_access")]
public class StorageFileAccess
{
    [Key]
    public Guid Id { get; set; }

    public Guid FileId { get; set; }

    [MaxLength(256)]
    public string? UserId { get; set; }

    [MaxLength(256)]
    public string? GroupId { get; set; }

    public StorageAccessLevel AccessLevel { get; set; }

    [ForeignKey("FileId")]
    public StorageFile File { get; set; } = null!;
}
