using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Storage.Models;

[Table("storage_nonces")]
public class StorageNonce
{
    [Key]
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    [Required]
    [MaxLength(256)]
    public string OperationName { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public bool Consumed { get; set; }
}
