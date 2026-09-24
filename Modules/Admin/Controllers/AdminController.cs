using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;

namespace SevenShows.Api.Modules.Admin.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SuperAdmin")]
[Tags("Administração Global do SaaS")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    public AdminController(AppDbContext context) => _context = context;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var now = DateTime.UtcNow;
        var tenantIds = await _context.Users.Where(u => u.Roles.Any(r => r.Id == "Tenant")).Select(u => u.Id).ToListAsync();
        var activeSubs = await _context.SaaSSubscriptions.CountAsync(s => s.Status == "Active");
        var overdueSubs = await _context.SaaSSubscriptions.CountAsync(s => s.Status == "PastDue");
        var shows = await _context.ArtistEvents.AsNoTracking().ToListAsync();
        var paidInvoices = await _context.SaaSInvoices.Where(i => i.Status == "PAYMENT_RECEIVED").ToListAsync();
        var gmv = shows.Where(e => !string.IsNullOrWhiteSpace(e.AsaasPaymentId)).Sum(e => e.TotalProposedPrice);
        var marketplaceRevenue = Math.Round(gmv * 0.05m, 2); // regra atualmente usada no motor de pagamentos
        var saasRevenue = paidInvoices.Sum(i => i.Value);

        return Ok(new {
            tenants = tenantIds.Count,
            activeTenants = await _context.Users.CountAsync(u => tenantIds.Contains(u.Id) && u.ProfileStatus == "Active"),
            contractors = await _context.Contratantes.CountAsync(),
            activeSubscriptions = activeSubs,
            overdueSubscriptions = overdueSubs,
            shows = shows.Count,
            gmv,
            marketplaceRevenue,
            saasRevenue,
            totalRevenue = marketplaceRevenue + saasRevenue,
            generatedAt = now
        });
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants()
    {
        var data = await _context.Users.AsNoTracking()
            .Where(u => u.Roles.Any(r => r.Id == "Tenant"))
            .Include(u => u.CurrentPlan)
            .OrderBy(u => u.Name)
            .Select(u => new {
                u.Id, u.Name, u.Email, u.MobilePhone, u.ProfileStatus, u.SubscriptionStatus,
                u.AsaasAccountStatus, u.Slug,
                planId = u.SaaSPlanId, planName = u.CurrentPlan != null ? u.CurrentPlan.Name : null,
                packages = _context.ArtistPackages.Count(p => p.UserId == u.Id),
                shows = _context.ArtistEvents.Count(e => e.UserId == u.Id)
            }).ToListAsync();
        return Ok(data);
    }

    [HttpGet("tenants/{id:guid}")]
    public async Task<IActionResult> Tenant(Guid id)
    {
        var u = await _context.Users.AsNoTracking().Include(x => x.CurrentPlan).FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        var subscription = await _context.SaaSSubscriptions.AsNoTracking().Include(s => s.SaaSPlan)
            .Where(s => s.UserId == id).OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync();
        return Ok(new { u.Id, u.Name, u.Email, u.MobilePhone, u.ProfileStatus, u.SubscriptionStatus, u.AsaasAccountStatus,
            u.AsaasWalletId, u.Slug, plan = u.CurrentPlan?.Name, subscription,
            packages = await _context.ArtistPackages.CountAsync(p => p.UserId == id),
            shows = await _context.ArtistEvents.CountAsync(e => e.UserId == id) });
    }

    [HttpGet("contractors")]
    public async Task<IActionResult> Contractors()
    {
        var data = await _context.Contratantes.AsNoTracking().OrderBy(c => c.NomeCompleto)
            .Select(c => new { c.Id, c.NomeCompleto, c.Email, c.Celular, c.CreatedAt,
                shows = _context.ArtistEvents.Count(e => e.ContractorId == c.Id) }).ToListAsync();
        return Ok(data);
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> Subscriptions()
    {
        var data = await _context.SaaSSubscriptions.AsNoTracking().Include(s => s.User).Include(s => s.SaaSPlan)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.UserId, tenantName = s.User != null ? s.User.Name : "", tenantEmail = s.User != null ? s.User.Email : "",
                planName = s.SaaSPlan != null ? s.SaaSPlan.Name : "", monthlyFee = s.SaaSPlan != null ? s.SaaSPlan.MonthlyFee : 0,
                s.PaymentMethod, s.Status, s.StartDate, s.EndDate, s.CreatedAt, s.AsaasSubscriptionId }).ToListAsync();
        return Ok(data);
    }

    [HttpGet("finance")]
    public async Task<IActionResult> Finance()
    {
        var invoices = await _context.SaaSInvoices.AsNoTracking().OrderByDescending(i => i.CreatedAt)
            .Select(i => new { id=i.Id, date=i.CreatedAt, type="SaaS", reference=i.AsaasInvoiceId, userId=(Guid?)i.UserId,
                description="Assinatura SaaS", gross=i.Value, platformRevenue=i.Value, status=i.Status, paymentMethod=i.PaymentMethod }).ToListAsync();
        var events = await _context.ArtistEvents.AsNoTracking().Include(e => e.User).Where(e => e.AsaasPaymentId != null)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { id=e.Id, date=e.CreatedAt, type="SHOW", reference=e.AsaasPaymentId!, userId=(Guid?)e.UserId,
                description=$"{e.Title} • {(e.User != null ? e.User.Name : "Artista")}", gross=e.TotalProposedPrice,
                platformRevenue=Math.Round(e.TotalProposedPrice * 0.05m, 2), status=e.Status, paymentMethod="Marketplace" }).ToListAsync();
        var movements = invoices.Cast<object>().Concat(events.Cast<object>()).ToList();
        var paidSaas = await _context.SaaSInvoices.Where(i => i.Status == "PAYMENT_RECEIVED").SumAsync(i => (decimal?)i.Value) ?? 0;
        var gmv = events.Sum(e => e.gross);
        return Ok(new { saasRevenue=paidSaas, gmv, marketplaceRevenue=Math.Round(gmv*0.05m,2), movements });
    }

    [HttpGet("shows")]
    public async Task<IActionResult> Shows()
    {
        var data = await _context.ArtistEvents.AsNoTracking().Include(e => e.User).OrderByDescending(e => e.EventDate)
            .Select(e => new { e.Id, e.Title, e.EventDate, e.VenueName, e.City, e.State, e.Status, e.ContractorId,
                e.ContractorName, artistName=e.User != null ? e.User.Name : "", e.TotalProposedPrice, e.AsaasPaymentId, e.CreatedAt }).ToListAsync();
        return Ok(data);
    }
}
