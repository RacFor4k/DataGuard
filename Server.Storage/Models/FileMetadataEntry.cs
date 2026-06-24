using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Storage.Models;

[Table("file_metadata_entries")]
public class FileMetadataEntry
{
    [Key]
    public Guid Id { get; set; }

    public Guid FileId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(4096)]
    public string Value { get; set; } = string.Empty;

    [ForeignKey("FileId")]
    public StorageFile File { get; set; } = null!;
}
