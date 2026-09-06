using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("artist_medias")]
public class ArtistMedia
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(20)]
    public string MediaType { get; set; } = "Photo"; // "Cover", "Photo" ou "Video"

    [Required]
    [StringLength(500)]
    public string MediaUrl { get; set; } = string.Empty; // Caminho local (/uploads/...) ou Link do YouTube

    [StringLength(255)]
    public string? Caption { get; set; } // Legenda estilo Instagram

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Relacionamento 1:N com o Usuário/Tenant
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ArtistMedia()
    {
        Id = Guid.NewGuid();
    }
}
