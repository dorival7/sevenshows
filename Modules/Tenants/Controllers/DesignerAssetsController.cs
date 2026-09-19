using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Tenants.Models;

namespace SevenShows.Api.Modules.Tenants.Controllers;

[ApiController]
[Route("api/tenants/designer-assets")]
[Authorize(Roles = "Tenant")]
[Tags("Seven Designer - Assets")]
public class DesignerAssetsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public DesignerAssetsController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var limit = await GetDesignerAssetLimit(userId.Value);
        var assets = await _context.DesignerAssets.AsNoTracking()
            .Where(x => x.UserId == userId.Value && !x.IsArchived)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new {
                x.Id, x.OriginalFileName, x.ContentType, x.OriginalUrl,
                x.BackgroundRemovedUrl, x.SizeBytes, x.Width, x.Height, x.CreatedAt
            }).ToListAsync();

        return Ok(new { items = assets, used = assets.Count, limit, canUpload = limit > 0 && assets.Count < limit });
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });
        if (file == null || file.Length == 0) return BadRequest(new { message = "Selecione uma imagem para enviar." });
        if (file.Length > MaxFileSizeBytes) return BadRequest(new { message = "A imagem excede o limite de 10 MB." });
        if (!AllowedContentTypes.Contains(file.ContentType)) return BadRequest(new { message = "Formato inválido. Use JPG, PNG ou WEBP." });

        var limit = await GetDesignerAssetLimit(userId.Value);
        if (limit <= 0) return BadRequest(new { message = "Seu plano não possui uploads habilitados para o Seven Designer." });

        var used = await CountVisibleAssets(userId.Value);
        if (used >= limit)
            return BadRequest(new { message = $"Limite de uploads atingido ({used}/{limit}). Exclua uma imagem para liberar espaço.", used, limit });

        var asset = new DesignerAsset {
            UserId = userId.Value,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            IsArchived = false
        };

        var extension = GetExtension(file.ContentType);
        var relativeDirectory = Path.Combine("uploads", "designer-assets", userId.Value.ToString("N"), "original");
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var physicalDirectory = Path.Combine(webRoot, relativeDirectory);
        Directory.CreateDirectory(physicalDirectory);

        var storedFileName = $"{asset.Id:N}{extension}";
        var physicalPath = Path.Combine(physicalDirectory, storedFileName);
        await using (var stream = System.IO.File.Create(physicalPath))
            await file.CopyToAsync(stream);

        asset.OriginalUrl = "/" + Path.Combine(relativeDirectory, storedFileName).Replace("\\", "/");

        _context.DesignerAssets.Add(asset);
        await _context.SaveChangesAsync();

        return Ok(new {
            asset.Id, asset.OriginalFileName, asset.ContentType, asset.OriginalUrl,
            asset.BackgroundRemovedUrl, asset.SizeBytes, asset.Width, asset.Height,
            asset.CreatedAt, used = used + 1, limit
        });
    }

    [HttpPost("{id:guid}/background-removed")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> SaveBackgroundRemoved(Guid id, [FromForm] IFormFile file)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null)
            return Unauthorized(new { message = "Usuário não identificado no token." });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "A imagem sem fundo não foi enviada." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "A imagem sem fundo excede o limite de 10 MB." });

        if (!string.Equals(file.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "A imagem sem fundo deve estar no formato PNG." });

        var asset = await _context.DesignerAssets
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);

        if (asset == null)
            return NotFound(new { message = "Imagem não encontrada." });

        // Um asset retirado de "Minhas imagens" é mantido apenas como dependência
        // histórica de cartazes. Não iniciamos novos derivados para ele.
        if (asset.IsArchived)
            return BadRequest(new
            {
                message = "Esta imagem foi removida de Minhas imagens e não pode gerar uma nova versão sem fundo."
            });

        var relativeDirectory = Path.Combine(
            "uploads",
            "designer-assets",
            userId.Value.ToString("N"),
            "removed");

        var webRoot = _environment.WebRootPath
                      ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var physicalDirectory = Path.Combine(webRoot, relativeDirectory);
        Directory.CreateDirectory(physicalDirectory);

        var storedFileName = $"{asset.Id:N}.png";
        var physicalPath = Path.Combine(physicalDirectory, storedFileName);

        // O mesmo asset possui apenas uma derivação sem fundo.
        // Reprocessar substitui o PNG anterior, sem criar nova linha e sem consumir cota.
        await using (var stream = new FileStream(
            physicalPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None))
        {
            await file.CopyToAsync(stream);
        }

        asset.BackgroundRemovedUrl =
            "/" + Path.Combine(relativeDirectory, storedFileName).Replace("\\", "/");

        await _context.SaveChangesAsync();

        return Ok(new
        {
            asset.Id,
            asset.OriginalFileName,
            asset.OriginalUrl,
            asset.BackgroundRemovedUrl,
            asset.SizeBytes,
            asset.Width,
            asset.Height,
            asset.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null) return Unauthorized(new { message = "Usuário não identificado no token." });

        var asset = await _context.DesignerAssets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);
        if (asset == null) return NotFound(new { message = "Imagem não encontrada." });

        var isUsed = await IsAssetReferencedByAnyPoster(userId.Value, asset.Id);

        if (isUsed)
        {
            asset.IsArchived = true;
            await _context.SaveChangesAsync();

            var usedArchived = await CountVisibleAssets(userId.Value);
            var limitArchived = await GetDesignerAssetLimit(userId.Value);
            return Ok(new {
                message = "Imagem removida de Minhas imagens e preservada nos cartazes que a utilizam.",
                archived = true, used = usedArchived, limit = limitArchived
            });
        }

        DeletePhysicalFile(asset.OriginalUrl);
        DeletePhysicalFile(asset.BackgroundRemovedUrl);
        _context.DesignerAssets.Remove(asset);
        await _context.SaveChangesAsync();

        var used = await CountVisibleAssets(userId.Value);
        var limit = await GetDesignerAssetLimit(userId.Value);
        return Ok(new { message = "Imagem excluída definitivamente.", archived = false, used, limit });
    }

    private Guid? GetAuthenticatedUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? User.FindFirstValue("nameid");
        return Guid.TryParse(rawId, out var userId) ? userId : null;
    }

    private async Task<int> GetDesignerAssetLimit(Guid userId)
    {
        var limit = await _context.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.CurrentPlan != null ? x.CurrentPlan.MaxDesignerAssets : 0)
            .FirstOrDefaultAsync();
        return Math.Max(0, limit);
    }

    private Task<int> CountVisibleAssets(Guid userId) =>
        _context.DesignerAssets.CountAsync(x => x.UserId == userId && !x.IsArchived);

    private async Task<bool> IsAssetReferencedByAnyPoster(Guid userId, Guid assetId)
    {
        var states = await _context.DesignerPosters.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.StateJson)
            .ToListAsync();

        return states.Any(x => StateJsonReferencesAsset(x, assetId));
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
            return true; // falha segura: não apaga se houver JSON corrompido
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

    private static string GetExtension(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => throw new InvalidOperationException("Tipo de imagem não suportado.")
    };

    private void DeletePhysicalFile(string? publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl)) return;
        var relativePath = publicUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var physicalPath = Path.Combine(webRoot, relativePath);
        if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
    }
}
