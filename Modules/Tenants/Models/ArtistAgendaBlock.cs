using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("artistagendablocks")] // Sincroniza com o nome físico do seu banco
public class ArtistAgendaBlock
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    // ====================================================================
    // RANGE CRONOLÓGICO: Substitui a data única por intervalo de tempo
    // ====================================================================
    [Required]
    public DateTime StartDate { get; set; } // Data Inicial do Recesso

    [Required]
    public DateTime EndDate { get; set; }   // Data Final do Recesso

    [Required]
    [StringLength(255)]
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ArtistAgendaBlock()
    {
        Id = Guid.NewGuid();
    }
}
