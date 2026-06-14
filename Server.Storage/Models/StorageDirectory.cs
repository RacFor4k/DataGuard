using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Storage.Models;

[Table("storage_directories")]
public class StorageDirectory
{
    [Key]
    public Guid DirectoryId { get; set; }

    public Guid OwnerId { get; set; }

    public Guid? ParentDirectoryId { get; set; }

    [Required]
    [MaxLength(1024)]
    public string DirectoryName { get; set; } = string.Empty;

    [Required]
    [MaxLength(4096)]
    public string NormalizedPath { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
}
