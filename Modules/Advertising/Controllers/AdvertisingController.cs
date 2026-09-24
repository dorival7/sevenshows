using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Advertising.Models;

namespace SevenShows.Api.Modules.Advertising.Controllers;

[ApiController]
[Route("api/advertising")]
public class AdvertisingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public AdvertisingController(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    [AllowAnonymous, HttpGet("public/home")]
    public async Task<IActionResult> Home()
    {
        var now = DateTime.UtcNow;
        var items = await _db.AdvertisingCampaigns.AsNoTracking()
            .Where(x => x.Status == "ACTIVE" && x.StartDate <= now && x.EndDate >= now)
            .OrderBy(x => x.Position).ThenByDescending(x => x.ContractValue).ToListAsync();
        var result = items.Select(x => PublicDto(x)).ToList();
        return Ok(result);
    }

    [AllowAnonymous, HttpGet("public/partner/{slug}")]
    public async Task<IActionResult> Partner(string slug)
    {
        var x = await _db.AdvertisingCampaigns.AsNoTracking().Where(c => c.Slug == slug && c.Status != "PAUSED")
            .OrderByDescending(c => c.EndDate).FirstOrDefaultAsync();
        return x == null ? NotFound() : Ok(PublicDto(x, true));
    }

    [AllowAnonymous, HttpPost("public/{id:guid}/impression")]
    public async Task<IActionResult> Impression(Guid id)
    {
        var affected = await _db.AdvertisingCampaigns.Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Impressions, x => x.Impressions + 1));
        return affected == 0 ? NotFound() : NoContent();
    }

    [AllowAnonymous, HttpPost("public/{id:guid}/click")]
    public async Task<IActionResult> Click(Guid id, [FromQuery] string channel = "ad")
    {
        var x = await _db.AdvertisingCampaigns.FirstOrDefaultAsync(c => c.Id == id);
        if (x == null) return NotFound();
        x.Clicks++;
        switch (channel.ToLowerInvariant()) { case "whatsapp": x.WhatsAppClicks++; break; case "instagram": x.InstagramClicks++; break; case "website": x.WebsiteClicks++; break; }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles="SuperAdmin"), HttpGet("admin")]
    public async Task<IActionResult> AdminList() => Ok(await _db.AdvertisingCampaigns.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync());

    [Authorize(Roles="SuperAdmin"), HttpPost("admin")]
    public async Task<IActionResult> Create([FromBody] AdvertisingCampaign input)
    {
        input.Id = Guid.NewGuid(); input.CreatedAt = input.UpdatedAt = DateTime.UtcNow; input.Slug = NormalizeSlug(input.Slug, input.AdvertiserName);
        _db.AdvertisingCampaigns.Add(input); await _db.SaveChangesAsync(); return Ok(input);
    }

    [Authorize(Roles="SuperAdmin"), HttpPut("admin/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdvertisingCampaign input)
    {
        var x = await _db.AdvertisingCampaigns.FindAsync(id); if (x == null) return NotFound();
        var impressions=x.Impressions; var clicks=x.Clicks; var wa=x.WhatsAppClicks; var ig=x.InstagramClicks; var web=x.WebsiteClicks; var created=x.CreatedAt;
        _db.Entry(x).CurrentValues.SetValues(input); x.Id=id; x.Impressions=impressions; x.Clicks=clicks; x.WhatsAppClicks=wa; x.InstagramClicks=ig; x.WebsiteClicks=web; x.CreatedAt=created; x.UpdatedAt=DateTime.UtcNow; x.Slug=NormalizeSlug(x.Slug,x.AdvertiserName);
        await _db.SaveChangesAsync(); return Ok(x);
    }

    [Authorize(Roles="SuperAdmin"), HttpDelete("admin/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) { var x=await _db.AdvertisingCampaigns.FindAsync(id); if(x==null)return NotFound(); _db.Remove(x); await _db.SaveChangesAsync(); return NoContent(); }

    [Authorize(Roles="SuperAdmin"), HttpPost("admin/upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        if (file.Length == 0 || file.Length > 8 * 1024 * 1024) return BadRequest(new {message="Imagem inválida ou maior que 8 MB."});
        var ext=Path.GetExtension(file.FileName).ToLowerInvariant(); if(!new[]{".jpg",".jpeg",".png",".webp"}.Contains(ext)) return BadRequest(new {message="Use JPG, PNG ou WEBP."});
        var dir=Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(),"wwwroot"),"uploads","advertising"); Directory.CreateDirectory(dir);
        var name=$"{Guid.NewGuid():N}{ext}"; await using var fs=System.IO.File.Create(Path.Combine(dir,name)); await file.CopyToAsync(fs);
        return Ok(new { url=$"/uploads/advertising/{name}" });
    }

    private static object PublicDto(AdvertisingCampaign x, bool details=false) => new { x.Id,x.AdvertiserName,x.Title,x.Description,x.DesktopImageUrl,x.MobileImageUrl,x.Position,x.DestinationType,x.DestinationUrl,x.WhatsApp,x.WhatsAppMessage,x.InstagramUrl,x.WebsiteUrl,x.Slug,x.City,x.State,x.LogoUrl, details };
    private static string NormalizeSlug(string? slug,string name) { var s=string.IsNullOrWhiteSpace(slug)?name:slug; s=s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD); s=new string(s.Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()); return string.Join('-',s.Split(new[]{' ','_','/'},StringSplitOptions.RemoveEmptyEntries)).Replace("--","-"); }
}
