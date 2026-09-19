using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("designer_assets")]
public class DesignerAsset
{
    [Key, Required]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required, StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string OriginalUrl { get; set; } = string.Empty;

    [StringLength(500)]
    public string? BackgroundRemovedUrl { get; set; }

    [Required]
    public long SizeBytes { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }

    [Required]
    public bool IsArchived { get; set; } = false;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DesignerAsset() => Id = Guid.NewGuid();
}
