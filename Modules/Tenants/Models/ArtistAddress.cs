using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("artist_addresses")]
public class ArtistAddress
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(255)]
    public string Street { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Number { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Complement { get; set; }

    [Required]
    [StringLength(100)]
    public string Neighborhood { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(2)]
    public string State { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string ZipCode { get; set; } = string.Empty; // CEP base para o motor de frete

    // Relacionamento 1:1 com o Usuário/Tenant
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ArtistAddress()
    {
        Id = Guid.NewGuid();
    }
}
