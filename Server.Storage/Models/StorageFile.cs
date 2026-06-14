using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Storage.Models;

[Table("storage_files")]
public class StorageFile
{
    [Key]
    public Guid FileId { get; set; }

    public Guid OwnerId { get; set; }

    public Guid? ParentDirectoryId { get; set; }

    [Required]
    [MaxLength(1024)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(4096)]
    public string NormalizedPath { get; set; } = string.Empty;

    public long Size { get; set; }

    [Required]
    [MaxLength(1024)]
    public string StorageKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string BucketName { get; set; } = "dataguard-storage";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    [Column(TypeName = "bytea")]
    public byte[]? ContentHash { get; set; }

    public ICollection<FileMetadataEntry> Metadata { get; set; } = new List<FileMetadataEntry>();
}
