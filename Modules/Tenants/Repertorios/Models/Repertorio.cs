using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;

namespace SevenShows.Api.Modules.Tenants.Repertorios.Models;

[Table("repertorios")]
public class Repertorio
{
    [Key, Required]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required, StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descricao { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<RepertorioMusica> Musicas { get; set; } = new();
}
