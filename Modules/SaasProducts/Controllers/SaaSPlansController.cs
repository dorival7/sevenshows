using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.SaasProducts.Models;

namespace SevenShows.Api.Modules.SaasProducts.Controllers;

[ApiController]
[Route("api/saas-products")]
[Tags("Produtos do SaaS (Planos)")]
public class SaaSPlansController : ControllerBase
{
  private readonly AppDbContext _context;

  public SaaSPlansController(AppDbContext context)
  {
    _context = context;
  }

  // 1. ROTA POST: Cadastra um novo plano dinâmico no banco de dados (PROTEGIDA)
  [HttpPost]
  [Authorize(Roles = "SuperAdmin")]
  public async Task<IActionResult> CreatePlan([FromBody] SaaSPlan plan)
  {
    if (string.IsNullOrWhiteSpace(plan.Name))
    {
      return BadRequest("O nome do plano é obrigatório.");
    }

    _context.SaaSPlans.Add(plan);
    await _context.SaveChangesAsync();

    return CreatedAtAction(nameof(GetPlanById), new { id = plan.Id }, plan);
  }

  // 2. ROTA GET: Recupera todos os planos cadastrados para listar na tela do músico (PÚBLICA)
  [HttpGet]
  [AllowAnonymous]
  public async Task<IActionResult> GetAllPlans()
  {
    var plans = await _context.SaaSPlans.ToListAsync();
    return Ok(plans);
  }

  // 3. ROTA GET (Interna): Busca um plano específico pelo ID único (PÚBLICA)
  [HttpGet("{id:guid}")]
  [AllowAnonymous]
  public async Task<IActionResult> GetPlanById(Guid id)
  {
    var plan = await _context.SaaSPlans.FindAsync(id);
    if (plan == null)
    {
      return NotFound("Plano não encontrado.");
    }
    return Ok(plan);
  }

  // 4. ROTA PUT: Atualiza as taxas e limites de um plano existente pelo ID (PROTEGIDA)
  [HttpPut("{id:guid}")]
  [Authorize(Roles = "SuperAdmin")]
  public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] SaaSPlan updatedPlan)
  {
    var existingPlan = await _context.SaaSPlans.FindAsync(id);
    if (existingPlan == null)
    {
      return NotFound("Plano não encontrado para edição.");
    }

    if (string.IsNullOrWhiteSpace(updatedPlan.Name))
    {
      return BadRequest("O nome do plano é obrigatório.");
    }

    if (updatedPlan.MonthlyFee < 0 || updatedPlan.DefaultTakeRatePercent < 0 || updatedPlan.DefaultTakeRatePercent > 100)
    {
      return BadRequest("Valores de mensalidade ou Take Rate inválidos.");
    }

    existingPlan.Name = updatedPlan.Name;
    existingPlan.Description = updatedPlan.Description;
    existingPlan.MonthlyFee = updatedPlan.MonthlyFee;
    existingPlan.DefaultTakeRatePercent = updatedPlan.DefaultTakeRatePercent;
    existingPlan.MaxShowsPerMonth = updatedPlan.MaxShowsPerMonth;
    existingPlan.MaxPhotosCount = updatedPlan.MaxPhotosCount;
    existingPlan.MaxVideosCount = updatedPlan.MaxVideosCount;
    existingPlan.MaxDesignerAssets = updatedPlan.MaxDesignerAssets;
    existingPlan.IsActive = updatedPlan.IsActive;

    await _context.SaveChangesAsync();

    return Ok(existingPlan);
  }
}
