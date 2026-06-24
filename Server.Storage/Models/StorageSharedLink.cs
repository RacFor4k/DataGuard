using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Storage.Models;

[Table("storage_shared_links")]
public class StorageSharedLink
{
    [Key]
    public Guid Id { get; set; }

    public Guid FileId { get; set; }

    public Guid OwnerId { get; set; }

    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public bool IsDirect { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Список разрешённых пользователей (JSON-массив GUID строк).
    /// Null означает «нет ограничений по пользователям».
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string>? AllowedUsers { get; set; }

    /// <summary>
    /// Список разрешённых групп (JSON-массив строк).
    /// Null означает «нет ограничений по группам».
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string>? AllowedGroups { get; set; }

    [ForeignKey("FileId")]
    public StorageFile File { get; set; } = null!;
}
