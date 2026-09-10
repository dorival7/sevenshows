using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Tenants.Models;
using SevenShows.Api.Modules.Contratantes.Dtos;

namespace SevenShows.Api.Modules.Contratantes.Controllers
{
  [ApiController]
  [Route("api/public/contratantes/eventos")]
  [AllowAnonymous] // 🔓 Aberto publicamente para o checkout dinâmico da Landing Page
  public class ContratanteEventsController : ControllerBase
  {
    private readonly AppDbContext _context;

    public ContratanteEventsController(AppDbContext context)
    {
      _context = context;
    }

    // ====================================================================
    // 🔥 POST: Gravação Atômica do Passo 2 e Vínculo com ContractorId
    // ====================================================================
    [HttpPost("propor-show")]
    public async Task<IActionResult> ProporShow([FromBody] CriarArtistEventDto model)
    {
      if (!ModelState.IsValid)
        return BadRequest(ModelState);

      try
      {
        // 1. CONVERSÃO DE TIPOS: Valida e converte o ContractorId vindo como string do DTO
        if (!Guid.TryParse(model.ContractorId, out Guid contractorGuid))
        {
          return BadRequest(new { success = false, message = "O identificador do contratante possui um formato inválido." });
        }

        // 🚀 CORREÇÃO CS1503: Se o model.ArtistPackageId já vem como Guid do DTO, não precisa de TryParse!
        if (model.ArtistPackageId == null || model.ArtistPackageId == Guid.Empty)
        {
          return BadRequest(new { success = false, message = "O identificador do pacote comercial é obrigatório." });
        }

        Guid packageGuid = model.ArtistPackageId.Value;

        // 2. HIDRATAÇÃO DO PACOTE COM O MÚSICO: Busca o pacote comercial legítimo do MariaDB
        var pacoteComercial = await _context.ArtistPackages
            .FirstOrDefaultAsync(p => p.Id == packageGuid);

        if (pacoteComercial == null)
          return NotFound(new { success = false, message = "O pacote comercial selecionado não foi localizado no banco." });

        // 3. HIDRATAÇÃO DO CONTRATANTE: Busca o cadastro unificado de quem está logado
        var contratante = await _context.Contratantes
            .FirstOrDefaultAsync(c => c.Id == contractorGuid);

        if (contratante == null)
          return NotFound(new { success = false, message = "Cadastro do contratante logado não localizado no sistema." });

        // 🚀 CORREÇÃO CS1061: Usa a propriedade real BasePrice do seu repositório do GitHub
        decimal precoBasePacote = pacoteComercial.BasePrice;
        decimal precoTotalCalculado = precoBasePacote + model.ExtraKmValueCharged + model.ExtraHoursValueCharged;
        // 5. MONTAGEM INTEGRAL HIDRATADA: Une os dados de logística com os dados seguros das tabelas
        var novoEvento = new ArtistEvent
        {
          UserId = pacoteComercial.UserId,      // 🚀 Extraído diretamente da tabela do banco!
          ContractorId = contractorGuid,        // 🔒 Vínculo forte de ID contra duplicidades
          Title = "Apresentação Artística - " + model.City,
          EventDate = model.EventDate,
          VenueName = model.VenueName,
          City = model.City,
          State = model.State,
          Status = "Pre_Approved",

          // Dados comerciais resgatados nativamente pelo .NET 10
          ArtistPackageId = pacoteComercial.Id,
          BasePackagePrice = precoBasePacote,
          ContractorName = contratante.NomeCompleto, // Copia o nome do perfil de contratantes

          // Variáveis mutáveis coletadas do formulário de logística
          EventType = model.EventType,
          DistanceKm = model.DistanceKm,
          ExtraKm = model.ExtraKm,
          ExtraKmValueCharged = model.ExtraKmValueCharged,
          RequestedDurationHours = model.RequestedDurationHours,
          ExtraHours = model.ExtraHours,
          ExtraHoursValueCharged = model.ExtraHoursValueCharged,
          TotalProposedPrice = precoTotalCalculado, // ⚡ Preço final blindado e calculado pelo backend
          Notes = model.Notes,
          CreatedAt = DateTime.UtcNow
        };

        // Força o rastreador a registrar a nova entidade primitiva de forma atômica
        _context.Entry(novoEvento).State = EntityState.Added;
        await _context.SaveChangesAsync();

        // Retorna o identificador gerado para o Vue 3 orquestrar os Passos 3 e 4
        return Ok(new
        {
          success = true,
          message = "Proposta de evento artístico hidratada e gravada com sucesso absoluto!",
          artistEventId = novoEvento.Id
        });
      }
      catch (Exception ex)
      {
        var erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        return StatusCode(500, new { success = false, message = "Falha ao persistir a proposta inicial no banco.", error = erroDetalhado });
      }
    }

    [HttpGet("listar-por-contratante/{contractorId:guid}")]
    public async Task<IActionResult> ListarPorContratante([FromRoute] Guid contractorId)
    {
      try
      {
        // Busca direta e filtrada por ContractorId com projeção leve e otimizada (AsNoTracking)
        var eventos = await _context.ArtistEvents
            .AsNoTracking()
            .Where(e => e.ContractorId == contractorId)
            .OrderByDescending(e => e.EventDate)
            .Select(e => new
            {
              Id = e.Id,
              // Busca o nome do Músico/Banda na tabela Users através do UserId do evento
              NomeMusico = _context.Users.Where(u => u.Id == e.UserId).Select(u => u.Name).FirstOrDefault() ?? "Atração Musical",
              Titulo = e.Title,
              DataEvento = e.EventDate, // Retorna o DateTime literal YYYY-MM-DDTHH:mm:ss
              Cidade = e.City,
              Estado = e.State,
              Status = e.Status,

              // Detalhamento financeiro enxuto para a listagem
              PrecoBase = e.BasePackagePrice,
              ValorKmExtra = e.ExtraKmValueCharged,
              ValorHoraExtra = e.ExtraHoursValueCharged,
              PrecoTotal = e.TotalProposedPrice
            })
            .ToListAsync();

        return Ok(new { success = true, eventos = eventos });
      }
      catch (Exception ex)
      {
        var erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        return StatusCode(500, new { success = false, message = "Falha ao recuperar a lista de shows agendados.", error = erroDetalhado });
      }
    }
  }
}

