using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Tenants.Repertorios.Dtos;
using SevenShows.Api.Modules.Tenants.Repertorios.Models;

namespace SevenShows.Api.Modules.Tenants.Repertorios.Controllers;

[ApiController]
[Route("api/tenants/repertorios")]
[Authorize(Roles = "Tenant")]
[Tags("Repertórios - Modo Palco")]
public class RepertoriosController : ControllerBase
{
    private readonly AppDbContext _context;

    public RepertoriosController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var items = await _context.Repertorios.AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new
            {
                x.Id, x.Nome, x.Descricao, x.CreatedAt, x.UpdatedAt,
                QuantidadeMusicas = x.Musicas.Count
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var item = await _context.Repertorios.AsNoTracking()
            .Where(x => x.Id == id && x.UserId == userId.Value)
            .Select(x => new
            {
                x.Id, x.Nome, x.Descricao, x.CreatedAt, x.UpdatedAt,
                Musicas = x.Musicas.OrderBy(m => m.Ordem).Select(m => new
                {
                    m.Id, m.Ordem, m.Musica, m.Artista, m.TomOriginal, m.TomEscolhido,
                    m.CifraCompleta, m.HtmlEstruturado, m.CreatedAt, m.UpdatedAt
                }).ToList()
            })
            .FirstOrDefaultAsync();

        return item == null ? NotFound(new { message = "Repertório não encontrado." }) : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CriarRepertorioRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var nome = NormalizarObrigatorio(request?.Nome, 150);
        if (nome == null) return BadRequest(new { message = "Informe o nome do repertório." });

        var item = new Repertorio
        {
            UserId = userId.Value,
            Nome = nome,
            Descricao = NormalizarOpcional(request?.Descricao, 500)
        };

        _context.Repertorios.Add(item);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, new { item.Id, item.Nome, item.Descricao, item.CreatedAt, item.UpdatedAt });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AtualizarRepertorioRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var item = await _context.Repertorios.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (item == null) return NotFound(new { message = "Repertório não encontrado." });

        var nome = NormalizarObrigatorio(request?.Nome, 150);
        if (nome == null) return BadRequest(new { message = "Informe o nome do repertório." });

        item.Nome = nome;
        item.Descricao = NormalizarOpcional(request?.Descricao, 500);
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { item.Id, item.Nome, item.Descricao, item.UpdatedAt });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var item = await _context.Repertorios.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (item == null) return NotFound(new { message = "Repertório não encontrado." });

        _context.Repertorios.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Repertório excluído." });
    }

    [HttpPost("{id:guid}/musicas")]
    public async Task<IActionResult> AddMusica(Guid id, [FromBody] AdicionarMusicaRepertorioRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var repertorio = await _context.Repertorios.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (repertorio == null) return NotFound(new { message = "Repertório não encontrado." });

        var erro = ValidarMusica(request);
        if (erro != null) return BadRequest(new { message = erro });

        var ultimaOrdem = await _context.RepertorioMusicas.Where(x => x.RepertorioId == id).MaxAsync(x => (int?)x.Ordem) ?? 0;
        var musica = CriarMusica(id, ultimaOrdem + 1, request);
        _context.RepertorioMusicas.Add(musica);
        repertorio.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToMusicaResponse(musica));
    }

    [HttpPut("{id:guid}/musicas/{musicaId:guid}")]
    public async Task<IActionResult> UpdateMusica(Guid id, Guid musicaId, [FromBody] AtualizarMusicaRepertorioRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var musica = await _context.RepertorioMusicas.Include(x => x.Repertorio)
            .FirstOrDefaultAsync(x => x.Id == musicaId && x.RepertorioId == id && x.Repertorio!.UserId == userId.Value);
        if (musica == null) return NotFound(new { message = "Música não encontrada no repertório." });

        var erro = ValidarMusica(request);
        if (erro != null) return BadRequest(new { message = erro });

        AplicarSnapshot(musica, request);
        musica.UpdatedAt = DateTime.UtcNow;
        musica.Repertorio!.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToMusicaResponse(musica));
    }

    [HttpDelete("{id:guid}/musicas/{musicaId:guid}")]
    public async Task<IActionResult> DeleteMusica(Guid id, Guid musicaId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var repertorio = await _context.Repertorios.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (repertorio == null) return NotFound(new { message = "Repertório não encontrado." });

        var musica = await _context.RepertorioMusicas.FirstOrDefaultAsync(x => x.Id == musicaId && x.RepertorioId == id);
        if (musica == null) return NotFound(new { message = "Música não encontrada no repertório." });

        _context.RepertorioMusicas.Remove(musica);
        await _context.SaveChangesAsync();
        await ReordenarSequencialmente(id);
        repertorio.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Música removida do repertório." });
    }

    [HttpPut("{id:guid}/musicas/reordenar")]
    public async Task<IActionResult> Reordenar(Guid id, [FromBody] ReordenarMusicasRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var repertorio = await _context.Repertorios.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (repertorio == null) return NotFound(new { message = "Repertório não encontrado." });

        var musicas = await _context.RepertorioMusicas.Where(x => x.RepertorioId == id).ToListAsync();
        var ids = request?.MusicaIds ?? new List<Guid>();
        if (ids.Count != musicas.Count || ids.Distinct().Count() != ids.Count || musicas.Any(x => !ids.Contains(x.Id)))
            return BadRequest(new { message = "A ordenação deve conter todas as músicas do repertório, sem duplicidades." });

        var mapa = ids.Select((musicaId, index) => new { musicaId, ordem = index + 1 }).ToDictionary(x => x.musicaId, x => x.ordem);
        foreach (var musica in musicas) musica.Ordem = mapa[musica.Id];
        repertorio.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Ordem atualizada." });
    }

    private Guid? GetAuthenticatedUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("nameid");
        return Guid.TryParse(rawId, out var userId) ? userId : null;
    }

    private static string? ValidarMusica(AdicionarMusicaRepertorioRequest? r)
    {
        if (r == null) return "Dados da música não informados.";
        if (string.IsNullOrWhiteSpace(r.Musica)) return "Informe o nome da música.";
        if (string.IsNullOrWhiteSpace(r.Artista)) return "Informe o artista.";
        if (string.IsNullOrWhiteSpace(r.TomEscolhido)) return "Informe o tom escolhido.";
        if (string.IsNullOrEmpty(r.CifraCompleta)) return "A cifra completa é obrigatória.";
        if (string.IsNullOrEmpty(r.HtmlEstruturado)) return "O HTML estruturado é obrigatório.";
        return null;
    }

    private static RepertorioMusica CriarMusica(Guid repertorioId, int ordem, AdicionarMusicaRepertorioRequest r)
    {
        var item = new RepertorioMusica { RepertorioId = repertorioId, Ordem = ordem };
        AplicarSnapshot(item, r);
        return item;
    }

    private static void AplicarSnapshot(RepertorioMusica item, AdicionarMusicaRepertorioRequest r)
    {
        item.Musica = Limitar(r.Musica.Trim(), 250);
        item.Artista = Limitar(r.Artista.Trim(), 250);
        item.TomOriginal = string.IsNullOrWhiteSpace(r.TomOriginal) ? null : Limitar(r.TomOriginal.Trim(), 20);
        item.TomEscolhido = Limitar(r.TomEscolhido.Trim(), 20);

        // REGRA CRÍTICA: não usar Trim/Replace/normalização nestes dois campos.
        // Espaços, tabs e quebras de linha preservam o alinhamento dos acordes.
        item.CifraCompleta = r.CifraCompleta;
        item.HtmlEstruturado = r.HtmlEstruturado;
    }

    private async Task ReordenarSequencialmente(Guid repertorioId)
    {
        var itens = await _context.RepertorioMusicas.Where(x => x.RepertorioId == repertorioId).OrderBy(x => x.Ordem).ThenBy(x => x.CreatedAt).ToListAsync();
        for (var i = 0; i < itens.Count; i++) itens[i].Ordem = i + 1;
    }

    private static object ToMusicaResponse(RepertorioMusica m) => new
    {
        m.Id, m.RepertorioId, m.Ordem, m.Musica, m.Artista, m.TomOriginal, m.TomEscolhido,
        m.CifraCompleta, m.HtmlEstruturado, m.CreatedAt, m.UpdatedAt
    };

    private static string? NormalizarObrigatorio(string? valor, int max) => string.IsNullOrWhiteSpace(valor) ? null : Limitar(valor.Trim(), max);
    private static string? NormalizarOpcional(string? valor, int max) => string.IsNullOrWhiteSpace(valor) ? null : Limitar(valor.Trim(), max);
    private static string Limitar(string valor, int max) => valor.Length <= max ? valor : valor[..max];
}
