using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("artist_packages")]
public class ArtistPackage
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty; // Título customizado pelo músico

    [Required]
    public int DurationMinutes { get; set; } // Duração padrão do show

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal BasePrice { get; set; } // Preço do show dentro do raio de isenção

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty; // Detalhes do que está incluso

    // Relacionamento 1:N com o Usuário/Tenant (Um músico pode ter até 3 pacotes)
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ArtistPackage()
    {
        Id = Guid.NewGuid();
    }
}
