using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("artist_comercial_settings")]
public class ArtistComercialSetting
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    public int FreeRadiusKm { get; set; } // Raio de isenção livre de frete (ex: 30)

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExtraKmValue { get; set; } // Valor cobrado por KM adicional

    [Required]
    public bool AcceptExtraHours { get; set; } // Se aceita estender o show

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExtraHourValue { get; set; } // Valor da hora adicional

    [Required]
    [StringLength(500)]
    public string AttendedRegions { get; set; } = string.Empty; // Texto livre das regiões atendidas

    // Relacionamento 1:1 com o Usuário/Tenant
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ArtistComercialSetting()
    {
        Id = Guid.NewGuid();
    }
}
