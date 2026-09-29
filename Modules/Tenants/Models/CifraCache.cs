namespace SevenShows.Api.Modules.Tenants.Models;

public class CifraCache
{
    public long Id { get; set; }
    public string NomeMusica { get; set; } = string.Empty;
    public string NomeMusicaNormalizado { get; set; } = string.Empty;
    public string NomeArtista { get; set; } = string.Empty;
    public string NomeArtistaNormalizado { get; set; } = string.Empty;
    public string TomOriginal { get; set; } = string.Empty;
    public string CifraCompleta { get; set; } = string.Empty;
    public string HtmlEstruturado { get; set; } = string.Empty;
    public string RelacionadasJson { get; set; } = "[]";
    public string SourceUrl { get; set; } = string.Empty;
    public int FormatoVersao { get; set; } = 1;
    public long SearchCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessAt { get; set; }
}
