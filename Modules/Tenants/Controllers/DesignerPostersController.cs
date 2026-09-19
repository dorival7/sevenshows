using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Tenants.Models;

namespace SevenShows.Api.Modules.Tenants.Controllers;

[ApiController]
[Route("api/tenants/designer-posters")]
[Authorize(Roles = "Tenant")]
[Tags("Seven Designer - Cartazes")]
public class DesignerPostersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public DesignerPostersController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetAuthenticatedUserId();

        if (userId == null)
            return Unauthorized(new
            {
                message = "Usuário não identificado no token."
            });

        var limit = await GetDesignerPosterLimit(userId.Value);

        // A lista visual deve mostrar TODOS:
        // - cartazes salvos
        // - o "Cartaz em criação" (draft)
        //
        // Porém o draft NÃO consome cota.
        var items = await _context.DesignerPosters
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.TemplateId,
                x.BackgroundId,
                x.PreviewUrl,
                x.EventId,
                x.IsDraft,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        // Somente cartazes efetivamente salvos
        // consomem a franquia do plano.
        var used = items.Count(x => !x.IsDraft);

        return Ok(new
        {
            items,
            used,
            limit,
            canCreate = limit > 0 && used < limit
        });
    }

    [HttpPost("active/ensure")]
    public async Task<IActionResult> EnsureActive()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var active = await _context.DesignerPosters.FirstOrDefaultAsync(x => x.UserId == userId.Value && x.IsActive);
        if (active == null)
        {
            active = CreateDraft(userId.Value);
            _context.DesignerPosters.Add(active);
            await _context.SaveChangesAsync();
        }
        return Ok(ToEditorResponse(active));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var poster = await _context.DesignerPosters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (poster == null) return NotFound(new { message = "Cartaz não encontrado." });

        return Ok(ToEditorResponse(poster));
    }

    [HttpPut("{id:guid}/autosave")]
    public async Task<IActionResult> AutoSave(Guid id, [FromBody] DesignerPosterAutoSaveRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });
        if (request == null) return BadRequest(new { message = "Dados do cartaz não informados." });
        if (!IsValidJson(request.StateJson)) return BadRequest(new { message = "StateJson inválido." });

        var poster = await _context.DesignerPosters.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (poster == null) return NotFound(new { message = "Cartaz não encontrado." });

        poster.StateJson = request.StateJson;
        poster.TemplateId = NormalizeTemplateId(request.TemplateId);
        poster.BackgroundId = NormalizeOptional(request.BackgroundId, 100);
        poster.PreviewUrl = NormalizeOptional(request.PreviewUrl, 500);
        poster.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            poster.Id,
            poster.Name,
            poster.IsDraft,
            poster.IsActive,
            poster.UpdatedAt,
            message = "Cartaz salvo automaticamente."
        });
    }

    [HttpPost("{id:guid}/save")]
    public async Task<IActionResult> Save(Guid id, [FromBody] DesignerPosterSaveRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var name = NormalizeName(request?.Name);
        if (name == null) return BadRequest(new { message = "Informe um nome para o cartaz." });

        var poster = await _context.DesignerPosters.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (poster == null) return NotFound(new { message = "Cartaz não encontrado." });

        var limit = await GetDesignerPosterLimit(userId.Value);
        var usedBeforeSave = await CountSavedPosters(userId.Value);

        if (poster.IsDraft)
        {
            if (limit <= 0)
                return BadRequest(new { message = "Seu plano não possui cartazes salvos habilitados no Seven Designer.", used = usedBeforeSave, limit });
            if (usedBeforeSave >= limit)
                return BadRequest(new { message = $"Limite de cartazes salvos atingido ({usedBeforeSave}/{limit}). Exclua um cartaz para liberar espaço.", used = usedBeforeSave, limit });
            poster.IsDraft = false;
        }

        poster.Name = name;
        poster.IsActive = true;
        poster.UpdatedAt = DateTime.UtcNow;
        await DeactivateOtherPosters(userId.Value, poster.Id);
        await _context.SaveChangesAsync();

        var used = await CountSavedPosters(userId.Value);
        return Ok(new
        {
            poster.Id,
            poster.Name,
            poster.IsDraft,
            poster.IsActive,
            poster.UpdatedAt,
            used,
            limit,
            canCreate = limit > 0 && used < limit,
            message = "Cartaz salvo. O salvamento automático continuará atualizando este cartaz."
        });
    }

    [HttpPost("new")]
    public async Task<IActionResult> NewPoster()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var activePosters = await _context.DesignerPosters
            .Where(x => x.UserId == userId.Value && x.IsActive).ToListAsync();

        var removedDraft = false;
        foreach (var active in activePosters)
        {
            if (active.IsDraft)
            {
                _context.DesignerPosters.Remove(active);
                removedDraft = true;
            }
            else active.IsActive = false;
        }

        var poster = CreateDraft(userId.Value);
        _context.DesignerPosters.Add(poster);
        await _context.SaveChangesAsync();

        if (removedDraft) await CleanupOrphanedArchivedAssets(userId.Value);
        return Ok(ToEditorResponse(poster));
    }

    // Consulta leve usada pela Agenda para decidir entre "Criar cartaz" e "Abrir cartaz".
    [HttpGet("by-event/{eventId:guid}")]
    public async Task<IActionResult> GetByEvent(Guid eventId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var poster = await _context.DesignerPosters
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value && x.EventId == eventId)
            .Select(x => new { x.Id })
            .FirstOrDefaultAsync();

        return Ok(new { exists = poster != null, posterId = poster != null ? poster.Id : (Guid?)null });
    }

    public sealed class NewAgendaPosterRequest
    {
        public Guid EventId { get; set; }
        public string? Name { get; set; }
    }

    // Fluxo Agenda -> Designer: um único cartaz por evento e por músico.
    // Se já existir, apenas o reabre; nunca reinjeta os dados da Agenda sobre a edição existente.
    [HttpPost("new-from-agenda")]
    public async Task<IActionResult> NewFromAgenda([FromBody] NewAgendaPosterRequest? request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });
        if (request == null || request.EventId == Guid.Empty)
            return BadRequest(new { message = "Evento não informado." });

        var existing = await _context.DesignerPosters
            .FirstOrDefaultAsync(x => x.UserId == userId.Value && x.EventId == request.EventId);

        if (existing != null)
        {
            await DeactivateOtherPosters(userId.Value, existing.Id);
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { poster = ToEditorResponse(existing), created = false });
        }

        await DeactivateOtherPosters(userId.Value, Guid.Empty);

        var poster = CreateDraft(userId.Value);
        poster.EventId = request.EventId;
        var requestedName = request.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedName))
            poster.Name = requestedName.Length > 120 ? requestedName[..120] : requestedName;

        _context.DesignerPosters.Add(poster);
        await _context.SaveChangesAsync();

        return Ok(new { poster = ToEditorResponse(poster), created = true });
    }

    [HttpPost("{id:guid}/open")]
    public async Task<IActionResult> Open(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var poster = await _context.DesignerPosters.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (poster == null) return NotFound(new { message = "Cartaz não encontrado." });

        await DeactivateOtherPosters(userId.Value, poster.Id);
        poster.IsActive = true;
        poster.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToEditorResponse(poster));
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var source = await _context.DesignerPosters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (source == null) return NotFound(new { message = "Cartaz não encontrado." });
        if (source.IsDraft) return BadRequest(new { message = "Salve o cartaz antes de duplicá-lo." });

        var limit = await GetDesignerPosterLimit(userId.Value);
        var used = await CountSavedPosters(userId.Value);
        if (limit <= 0 || used >= limit)
            return BadRequest(new { message = $"Limite de cartazes salvos atingido ({used}/{limit}).", used, limit });

        await DeactivateOtherPosters(userId.Value, Guid.Empty);

        var copy = new DesignerPoster
        {
            UserId = userId.Value,
            Name = BuildCopyName(source.Name),
            TemplateId = source.TemplateId,
            BackgroundId = source.BackgroundId,
            StateJson = source.StateJson,
            PreviewUrl = source.PreviewUrl,
            IsDraft = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.DesignerPosters.Add(copy);
        await _context.SaveChangesAsync();
        used++;

        return Ok(new { poster = ToEditorResponse(copy), used, limit, canCreate = used < limit });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var poster = await _context.DesignerPosters.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (poster == null) return NotFound(new { message = "Cartaz não encontrado." });

        var wasActive = poster.IsActive;
        _context.DesignerPosters.Remove(poster);
        await _context.SaveChangesAsync();

        DesignerPoster? active = null;
        if (wasActive)
        {
            active = CreateDraft(userId.Value);
            _context.DesignerPosters.Add(active);
            await _context.SaveChangesAsync();
        }

        // Só apaga assets que já saíram de "Minhas imagens" E ficaram sem referência.
        // Se outro cartaz ainda usa a foto, ela permanece intacta.
        var deletedOrphanAssets = await CleanupOrphanedArchivedAssets(userId.Value);

        var used = await CountSavedPosters(userId.Value);
        var limit = await GetDesignerPosterLimit(userId.Value);
        return Ok(new
        {
            message = "Cartaz excluído.",
            used,
            limit,
            canCreate = limit > 0 && used < limit,
            activePoster = active == null ? null : ToEditorResponse(active),
            deletedOrphanAssets
        });
    }

    private DesignerPoster CreateDraft(Guid userId) => new()
    {
        UserId = userId,
        Name = "Cartaz em criação",
        TemplateId = "sertanejo-sunset",
        StateJson = "{}",
        IsDraft = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private Guid? GetAuthenticatedUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? User.FindFirstValue("nameid");
        return Guid.TryParse(rawId, out var userId) ? userId : null;
    }

    private async Task<int> GetDesignerPosterLimit(Guid userId)
    {
        var limit = await _context.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.CurrentPlan != null ? x.CurrentPlan.MaxDesignerPosters : 0)
            .FirstOrDefaultAsync();
        return Math.Max(0, limit);
    }

    private Task<int> CountSavedPosters(Guid userId) =>
        _context.DesignerPosters.CountAsync(x => x.UserId == userId && !x.IsDraft);

    private async Task DeactivateOtherPosters(Guid userId, Guid exceptId)
    {
        var others = await _context.DesignerPosters
            .Where(x => x.UserId == userId && x.IsActive && x.Id != exceptId).ToListAsync();
        foreach (var item in others) item.IsActive = false;
    }

    private async Task<int> CleanupOrphanedArchivedAssets(Guid userId)
    {
        var assets = await _context.DesignerAssets
            .Where(x => x.UserId == userId && x.IsArchived).ToListAsync();
        if (assets.Count == 0) return 0;

        var states = await _context.DesignerPosters.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.StateJson).ToListAsync();

        var removed = 0;
        foreach (var asset in assets)
        {
            if (states.Any(x => StateJsonReferencesAsset(x, asset.Id))) continue;

            DeletePhysicalFile(asset.OriginalUrl);
            DeletePhysicalFile(asset.BackgroundRemovedUrl);
            _context.DesignerAssets.Remove(asset);
            removed++;
        }

        if (removed > 0) await _context.SaveChangesAsync();
        return removed;
    }

    private static bool StateJsonReferencesAsset(string? json, Guid assetId)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonElementReferencesAsset(doc.RootElement, assetId);
        }
        catch (JsonException)
        {
            return true; // falha segura: preserva em vez de quebrar cartaz
        }
    }

    private static bool JsonElementReferencesAsset(JsonElement element, Guid assetId)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in element.EnumerateObject())
            {
                if (p.NameEquals("assetId") &&
                    p.Value.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(p.Value.GetString(), out var id) && id == assetId)
                    return true;

                if (JsonElementReferencesAsset(p.Value, assetId)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (JsonElementReferencesAsset(item, assetId)) return true;
        }
        return false;
    }

    private void DeletePhysicalFile(string? publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl)) return;
        var relativePath = publicUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var physicalPath = Path.Combine(webRoot, relativePath);
        if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
    }

    private static object ToEditorResponse(DesignerPoster poster) => new
    {
        poster.Id,
        poster.EventId,
        poster.Name,
        poster.TemplateId,
        poster.BackgroundId,
        poster.StateJson,
        poster.PreviewUrl,
        poster.IsDraft,
        poster.IsActive,
        poster.CreatedAt,
        poster.UpdatedAt
    };

    private static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { using var _ = JsonDocument.Parse(json); return true; }
        catch (JsonException) { return false; }
    }

    private static string NormalizeTemplateId(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "sertanejo-sunset" : value.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= 150 ? normalized : normalized[..150];
    }

    private static string BuildCopyName(string sourceName)
    {
        const string suffix = " - Cópia";
        var maxSourceLength = 150 - suffix.Length;
        var safeSource = sourceName.Length <= maxSourceLength ? sourceName : sourceName[..maxSourceLength];
        return safeSource + suffix;
    }
}

public class DesignerPosterAutoSaveRequest
{
    public string StateJson { get; set; } = "{}";
    public string? TemplateId { get; set; }
    public string? BackgroundId { get; set; }
    public string? PreviewUrl { get; set; }
}

public class DesignerPosterSaveRequest
{
    public string Name { get; set; } = string.Empty;
}
