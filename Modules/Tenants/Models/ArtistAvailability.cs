using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("artist_availabilities")]
public class ArtistAvailability
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    public int DayOfWeek { get; set; } // 0 (Domingo) a 6 (Sábado)

    [Required]
    public TimeSpan StartTime { get; set; } // Horário de início disponível (ex: 18:00:00)

    [Required]
    public TimeSpan EndTime { get; set; } // Horário de término disponível (ex: 02:00:00)

    [Required]
    public bool IsAvailable { get; set; } = true; // Botão liga/desliga para o dia específico

    public ArtistAvailability()
    {
        Id = Guid.NewGuid();
    }
}
