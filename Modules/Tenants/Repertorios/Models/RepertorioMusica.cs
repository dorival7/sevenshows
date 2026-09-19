using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenShows.Api.Modules.Tenants.Repertorios.Models;

[Table("repertorio_musicas")]
public class RepertorioMusica
{
    [Key, Required]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid RepertorioId { get; set; }

    [ForeignKey(nameof(RepertorioId))]
    public Repertorio? Repertorio { get; set; }

    [Required]
    public int Ordem { get; set; }

    [Required, StringLength(250)]
    public string Musica { get; set; } = string.Empty;

    [Required, StringLength(250)]
    public string Artista { get; set; } = string.Empty;

    [StringLength(20)]
    public string? TomOriginal { get; set; }

    [Required, StringLength(20)]
    public string TomEscolhido { get; set; } = string.Empty;

    // Snapshot textual: espaços e quebras de linha são parte do conteúdo.
    [Required, Column(TypeName = "longtext")]
    public string CifraCompleta { get; set; } = string.Empty;

    // Mantido para futuras transposições sem nova busca externa.
    [Required, Column(TypeName = "longtext")]
    public string HtmlEstruturado { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
