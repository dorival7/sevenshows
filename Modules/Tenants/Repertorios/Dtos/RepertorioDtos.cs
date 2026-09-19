namespace SevenShows.Api.Modules.Tenants.Repertorios.Dtos;

public class CriarRepertorioRequest
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}

public class AtualizarRepertorioRequest : CriarRepertorioRequest { }

public class AdicionarMusicaRepertorioRequest
{
    public string Musica { get; set; } = string.Empty;
    public string Artista { get; set; } = string.Empty;
    public string? TomOriginal { get; set; }
    public string TomEscolhido { get; set; } = string.Empty;
    public string CifraCompleta { get; set; } = string.Empty;
    public string HtmlEstruturado { get; set; } = string.Empty;
}

public class AtualizarMusicaRepertorioRequest : AdicionarMusicaRepertorioRequest { }

public class ReordenarMusicasRequest
{
    public List<Guid> MusicaIds { get; set; } = new();
}
