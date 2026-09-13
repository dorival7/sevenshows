namespace SevenShows.Api.Modules.Tenants.Models;

public class PublicArtistDto
{
    public Guid Id { get; set; }
    public string NomeBanda { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string EstiloMusical { get; set; } = string.Empty;
    public string FormatoArtístico { get; set; } = string.Empty;
    public string Slogan { get; set; } = string.Empty;
    public string CidadeAtendida { get; set; } = string.Empty;
    
    // 🆕 ADICIONADO: Campo comercial para trafegar a região de abrangência para o card
    public string RegiaoAtendida { get; set; } = string.Empty;
    
    public string State { get; set; } = string.Empty;
    public string FotoCapaUrl { get; set; } = string.Empty;
    public decimal PrecoBase { get; set; }
}
