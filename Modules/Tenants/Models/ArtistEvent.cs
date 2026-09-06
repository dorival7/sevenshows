using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("artistevents")] // Vincula exatamente ao nome físico do seu dump SQL
public class ArtistEvent
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    // ====================================================================
    // COLUNAS ORIGINAIS (PRESERVADAS PARA A DASHBOARD SE MANTER 100% VIVA)
    // ====================================================================
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime EventDate { get; set; }

    [Required]
    public string VenueName { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string State { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = "Pending";

    // ====================================================================
    // NOVAS COLUNAS ADICIONAIS (ESTENDIDAS PARA A LOGÍSTICA REAL DA AGENDA)
    // ====================================================================
    public Guid? ArtistPackageId { get; set; } // Nullable para não quebrar os registros antigos do dump

    [ForeignKey(nameof(ArtistPackageId))]
    public ArtistPackage? ArtistPackage { get; set; }

    [StringLength(150)]
    public string? ContractorName { get; set; }

    [StringLength(100)]
    public string? EventType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BasePackagePrice { get; set; } = 0.00m;

    public int DistanceKm { get; set; } = 0;

    public int ExtraKm { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExtraKmValueCharged { get; set; } = 0.00m;

    public int RequestedDurationHours { get; set; } = 0;

    public int ExtraHours { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExtraHoursValueCharged { get; set; } = 0.00m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalProposedPrice { get; set; } = 0.00m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }

    public ArtistEvent()
    {
        Id = Guid.NewGuid();
    }
}
