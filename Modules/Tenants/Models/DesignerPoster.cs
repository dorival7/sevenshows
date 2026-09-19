using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("designer_posters")]
public class DesignerPoster
{
    [Key, Required]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = "Cartaz em criação";

    [Required, StringLength(100)]
    public string TemplateId { get; set; } = "sertanejo-sunset";

    [StringLength(100)]
    public string? BackgroundId { get; set; }

    [Required, Column(TypeName = "longtext")]
    public string StateJson { get; set; } = "{}";

    [StringLength(500)]
    public string? PreviewUrl { get; set; }

    [Required]
    public bool IsDraft { get; set; } = true;

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DesignerPoster() => Id = Guid.NewGuid();
}
