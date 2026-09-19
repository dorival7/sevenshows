using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Tenants.Models;
using System.Security.Claims;

namespace SevenShows.Api.Modules.Tenants.Controllers;

// DTOs de Entrada e Validação
public record SaveAvailabilityRequest(int DayOfWeek, string StartTime, string EndTime, bool IsAvailable);
public record CreateBlockRequest(DateTime StartDate, DateTime EndDate, string? Reason);

public record ShowRequestItemResponse(
    Guid Id,
    string ContractorName,
    string EventName,
    string EventType,
    DateTime EventDate,
    string PackageTitle,
    decimal BasePackagePrice,
    int DistanceKm,
    int ExtraKm,
    decimal ExtraKmValueCharged,
    int RequestedDurationHours,
    int ExtraHours,
    decimal ExtraHoursValueCharged,
    decimal TotalProposedPrice,
    string Status,
    string Notes // INJETADO: Garante o transporte do texto da modal até as abas do Vue
);

public record ActionWithNotesRequest(string Notes);

[ApiController]
[Route("api/tenants/agenda")]
[Authorize(Roles = "Tenant")]
[Tags("Agenda e Disponibilidade do Artista")]
public class AgendaController : ControllerBase
{
  private readonly AppDbContext _context;

  public AgendaController(AppDbContext context)
  {
    _context = context;
  }

  // 1. GET: Retorna as configurações da grade semanal padrão do músico
  [HttpGet("availability")]
  public async Task<IActionResult> GetAvailability()
  {
    var tenantIdLogado = ObterTenantIdLogado();

    // INJETADO AsNoTracking: Reduz o consumo de memória RAM do servidor para quase zero
    var grade = await _context.ArtistAvailabilities
        .AsNoTracking()
        .Where(a => a.UserId == tenantIdLogado)
        .OrderBy(a => a.DayOfWeek)
        .ToListAsync();

    return Ok(grade);
  }

  // 2. PUT: Atualiza ou cria uma regra para um dia específico da semana
  [HttpPut("availability")]
  public async Task<IActionResult> SaveAvailability([FromBody] SaveAvailabilityRequest model)
  {
    if (model.DayOfWeek < 0 || model.DayOfWeek > 6)
      return BadRequest("Dia da semana inválido. Use de 0 (Domingo) a 6 (Sábado).");

    var tenantIdLogado = ObterTenantIdLogado();

    var registro = await _context.ArtistAvailabilities
        .FirstOrDefaultAsync(a => a.UserId == tenantIdLogado && a.DayOfWeek == model.DayOfWeek);

    if (registro == null)
    {
      registro = new ArtistAvailability { UserId = tenantIdLogado, DayOfWeek = model.DayOfWeek };
      _context.ArtistAvailabilities.Add(registro);
    }

    registro.StartTime = TimeSpan.Parse(model.StartTime);
    registro.EndTime = TimeSpan.Parse(model.EndTime);
    registro.IsAvailable = model.IsAvailable;

    await _context.SaveChangesAsync();
    return Ok(new { message = "Disponibilidade semanal atualizada com sucesso!" });
  }

  // 3. GET: Retorna todas as travas e exceções manuais configuradas (Alimenta o calendário do Velzon)
  [HttpGet("blocks")]
  public async Task<IActionResult> GetBlocks()
  {
    var tenantIdLogado = ObterTenantIdLogado();

    // CORRIGIDO: Substituído BlockDate por StartDate para alinhar com o novo modelo de Range
    var blocos = await _context.ArtistAgendaBlocks
        .AsNoTracking()
        .Where(b => b.UserId == tenantIdLogado)
        .OrderBy(b => b.StartDate) // Ordena pela data inicial do recesso
        .ToListAsync();

    return Ok(blocos);
  }

  // 4. POST: Trava manualmente uma data ou horário no calendário (Emergência/Férias)
  [HttpPost("blocks")]
  public async Task<IActionResult> CreateBlock([FromBody] CreateBlockRequest model)
  {
    var tenantIdLogado = ObterTenantIdLogado();

    if (model.StartDate.Date < DateTime.UtcNow.Date)
        return BadRequest("A data inicial do recesso não pode ser inferior ao dia de hoje.");

    if (model.EndDate.Date < model.StartDate.Date)
        return BadRequest("A data final do recesso não pode ser anterior à data de início.");

    // Instancia o recesso gravando o intervalo completo no banco
    var novoBloco = new ArtistAgendaBlock
    {
      Id = Guid.NewGuid(),
      UserId = tenantIdLogado,
      StartDate = model.StartDate.Date, // Sincronizado com os campos da nova migration
      EndDate = model.EndDate.Date,     // Sincronizado com os campos da nova migration
      Reason = !string.IsNullOrEmpty(model.Reason) ? model.Reason : "Recesso/Bloqueio Manual",
      CreatedAt = DateTime.UtcNow
    };

    _context.ArtistAgendaBlocks.Add(novoBloco);
    await _context.SaveChangesAsync();

    return Ok(new { message = "Intervalo de recesso bloqueado com sucesso no seu calendário!", id = novoBloco.Id });
  }

  // ====================================================================
  // 4.1. PUT: ATUALIZAR / EDITAR UM INTERVALO DE RECESSO EXISTENTE
  // ====================================================================
  // PUT: api/tenants/agenda/blocks/{id:guid}
  [HttpPut("blocks/{id:guid}")]
  public async Task<IActionResult> UpdateBlock(Guid id, [FromBody] CreateBlockRequest model)
  {
    var tenantIdLogado = ObterTenantIdLogado();

    // 1. Busca o bloqueio garantindo que pertença ao músico autenticado
    var blocoExistente = await _context.ArtistAgendaBlocks
        .FirstOrDefaultAsync(b => b.Id == id && b.UserId == tenantIdLogado);

    if (blocoExistente == null)
        return NotFound("Intervalo de recesso não localizado ou acesso negado.");

    // 2. Validações de consistência cronológica de segurança
    if (model.StartDate.Date < DateTime.UtcNow.Date)
        return BadRequest("A nova data inicial do recesso não pode ser inferior ao dia de hoje.");

    if (model.EndDate.Date < model.StartDate.Date)
        return BadRequest("A nova data final do recesso não pode ser anterior à data de início.");

    // 3. Atualiza os campos na linha física do MariaDB
    blocoExistente.StartDate = model.StartDate.Date;
    blocoExistente.EndDate = model.EndDate.Date;
    blocoExistente.Reason = !string.IsNullOrEmpty(model.Reason) ? model.Reason : "Recesso/Bloqueio Modificado";

    await _context.SaveChangesAsync();
    return Ok(new { message = "Intervalo de recesso atualizado com sucesso!" });
  }

  // 5. DELETE: Remove uma trava manual (Libera o horário de volta na vitrine pública)
  [HttpDelete("blocks/{id:guid}")]
  public async Task<IActionResult> DeleteBlock(Guid id)
  {
    var tenantIdLogado = ObterTenantIdLogado();
    
    var bloco = await _context.ArtistAgendaBlocks
        .FirstOrDefaultAsync(b => b.Id == id && b.UserId == tenantIdLogado);

    if (bloco == null)
      return NotFound("Bloqueio de agenda não encontrado ou acesso negado.");

    _context.ArtistAgendaBlocks.Remove(bloco);
    await _context.SaveChangesAsync();

    return Ok(new { message = "Intervalo de recesso liberado com sucesso para novos agendamentos!" });
  }

  private Guid ObterTenantIdLogado()
  {
    var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Guid.Parse(claimId!);
  }

  // ====================================================================
  // NOVO: ETAPA 5.5 - VISÃO UNIFICADA DE CALENDÁRIO PARA O VELZON (VUE 3)
  // ====================================================================
  // GET: api/tenants/agenda/calendar-view?month=12&year=2026
  [HttpGet("calendar-view")]
  public async Task<IActionResult> GetCalendarView([FromQuery] int month, [FromQuery] int year)
  {
    if (month < 1 || month > 12) return BadRequest("Mês inválido. Use valores de 1 a 12.");
    if (year < 2026) return BadRequest("Ano inválido.");

    var tenantIdLogado = ObterTenantIdLogado();

    // 1. Busca todas as tabelas usando AsNoTracking para performance máxima
    var disponibilidadesPadrão = await _context.ArtistAvailabilities
        .AsNoTracking()
        .Where(a => a.UserId == tenantIdLogado)
        .ToListAsync();

    var bloqueiosDoMes = await _context.ArtistAgendaBlocks
        .AsNoTracking()
        .Where(b => b.UserId == tenantIdLogado && 
              ((b.StartDate.Month == month && b.StartDate.Year == year) || (b.EndDate.Month == month && b.EndDate.Year == year)))
        .ToListAsync();

    // FILTRAGEM EVOLUÍDA: Ignora as propostas rejeitadas (Rejected) para liberar o dia no calendário de forma automática
    var showsDoMes = await _context.ArtistEvents
        .AsNoTracking()
        .Include(e => e.ArtistPackage)
        .Where(e => e.UserId == tenantIdLogado
               && e.EventDate.Month == month
               && e.EventDate.Year == year
               && e.Status != "Rejected") // Remove os recusados da árvore visual
        .ToListAsync();

    // Logo opcional do contratante: carregado em lote para evitar N+1 queries.
    var contractorIds = showsDoMes
        .Where(e => e.ContractorId.HasValue)
        .Select(e => e.ContractorId!.Value)
        .Distinct()
        .ToList();

    var logosContratantes = contractorIds.Count == 0
        ? new Dictionary<Guid, string?>()
        : await _context.Contratantes
            .AsNoTracking()
            .Where(c => contractorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.LogoUrl);

    int diasNoMes = DateTime.DaysInMonth(year, month);
    var listaEventosCalendario = new List<object>();

    // 2. Loop cronológico aplicando o novo mapa de cores comercial
    for (int dia = 1; dia <= diasNoMes; dia++)
    {
      var dataAtual = new DateTime(year, month, dia);
      int diaDaSemanaInt = (int)dataAtual.DayOfWeek;

      var showsDoDia = showsDoMes.Where(e => e.EventDate.Date == dataAtual.Date).ToList();
      var bloqueioExistente = bloqueiosDoMes.FirstOrDefault(b => dataAtual.Date >= b.StartDate.Date && dataAtual.Date <= b.EndDate.Date);

      // DETERMINAÇÃO DO ESTADO DA CÉLULA MESTRE (Comando hierárquico)
      string corMestre = "light";
      if (showsDoDia.Any(s => s.Status == "Confirmed")) corMestre = "info"; // Azul (Confirmado)
      else if (showsDoDia.Any(s => s.Status == "Pre_Approved")) corMestre = "warning"; // Laranja (Aguardando Pix)
      else if (showsDoDia.Any(s => s.Status == "In_Negotiation")) corMestre = "secondary"; // ROXO: Aba Negociando!
      else if (bloqueioExistente != null) corMestre = "danger"; // Vermelho (Trava manual)
      else
      {
        var regra = disponibilidadesPadrão.FirstOrDefault(a => a.DayOfWeek == diaDaSemanaInt);
        if (regra != null && regra.IsAvailable) corMestre = "success"; // Verde (Disponível)
      }

      var subEventosJson = new List<object>();

      foreach (var show in showsDoDia)
      {
        // Define a cor da badge individual com base na nova mesa de negociações
        string badgeColor = "info";
        if (show.Status == "Pre_Approved") badgeColor = "warning";
        else if (show.Status == "In_Negotiation") badgeColor = "secondary"; // Roxo para a badge

        subEventosJson.Add(new
        {
          id = show.Id,
          title = show.Title ?? "Show sem Nome",
          status = show.Status,
          contractorName = show.ContractorName ?? "Não Informado",
          contractorLogoUrl = show.ContractorId.HasValue && logosContratantes.TryGetValue(show.ContractorId.Value, out var logoUrl) ? logoUrl : null,
          eventType = show.EventType ?? "Show",
          eventDate = show.EventDate,
          venueName = show.VenueName ?? "Local Não Informado",
          city = show.City ?? "Cidade Não Informada",
          state = show.State ?? "UF",
          packageTitle = show.ArtistPackage != null ? show.ArtistPackage.Title : "Pacote Padrão",
          basePackagePrice = show.BasePackagePrice,
          distanceKm = show.DistanceKm,
          extraKm = show.ExtraKm,
          extraKmValueCharged = show.ExtraKmValueCharged,
          requestedDurationHours = show.RequestedDurationHours,
          extraHours = show.ExtraHours,
          extraHoursValueCharged = show.ExtraHoursValueCharged,
          totalProposedPrice = show.TotalProposedPrice,
          notes = show.Notes ?? "", // Envia a mensagem salva da modal para o Vue renderizar
          color = badgeColor
        });
      }

      if (!showsDoDia.Any() && bloqueioExistente != null)
      {
        subEventosJson.Add(new
        {
          id = bloqueioExistente.Id,
          title = !string.IsNullOrEmpty(bloqueioExistente.Reason) ? bloqueioExistente.Reason : "Horário Travado",
          status = "Blocked",
          color = "danger"
        });
      }

      listaEventosCalendario.Add(new
      {
        date = dataAtual.ToString("yyyy-MM-dd"),
        dayOfWeek = diaDaSemanaInt,
        color = corMestre,
        cellDefaultTitle = bloqueioExistente != null
            ? (!string.IsNullOrEmpty(bloqueioExistente.Reason) ? bloqueioExistente.Reason : "Horário Travado")
            : (disponibilidadesPadrão.FirstOrDefault(a => a.DayOfWeek == diaDaSemanaInt)?.IsAvailable == true ? "Disponível para Shows" : "Folga Padrão"),
        events = subEventosJson
      });
    }

    return Ok(listaEventosCalendario);
  }

  // ====================================================================
  // 5.6. GET: Listar Solicitações de Shows Pendentes (Logística Real)
  // ====================================================================
  // GET: api/tenants/agenda/requests
  [HttpGet("requests")]
  public async Task<IActionResult> GetPendingShowRequests()
  {
      var tenantIdLogado = ObterTenantIdLogado();

      // Alterado o .Where para trazer Pending, In_Negotiation e Rejected de uma só vez
      var solicitacoes = await _context.ArtistEvents
        .AsNoTracking()
        .Include(e => e.ArtistPackage) 
        .Where(e => e.UserId == tenantIdLogado && 
              (e.Status == "Pending" || e.Status == "In_Negotiation" || e.Status == "Rejected"))
        .OrderBy(e => e.EventDate)
        .Select(e => new ShowRequestItemResponse(
            e.Id,
            e.ContractorName ?? "Contratante Anônimo",
            e.Title ?? "Evento sem Nome", 
            e.EventType ?? "Não Informado",
            e.EventDate,
            e.ArtistPackage != null ? e.ArtistPackage.Title : "Pacote Personalizado",
            e.BasePackagePrice,
            e.DistanceKm,
            e.ExtraKm,
            e.ExtraKmValueCharged,
            e.RequestedDurationHours,
            e.ExtraHours,
            e.ExtraHoursValueCharged,
            e.TotalProposedPrice,
            e.Status,
            e.Notes ?? "" // INJETADO: Lê o texto real do MariaDB e joga na rede para o Vue 3
        ))
        .ToListAsync();

      return Ok(solicitacoes);
  }

  // ====================================================================
  // 5.7. POST: Músico Aceita/Pré-Aprova Proposta (Muda para Pre_Approved)
  // ====================================================================
  // POST: api/tenants/agenda/requests/{id}/accept
  [HttpPost("requests/{id:guid}/accept")]
  public async Task<IActionResult> AcceptShowRequest(Guid id)
  {
    var tenantIdLogado = ObterTenantIdLogado();

    // Localiza a proposta garantindo que ela pertença ao músico autenticado
    var proposta = await _context.ArtistEvents
        .FirstOrDefaultAsync(e => e.Id == id && e.UserId == tenantIdLogado);

    if (proposta == null)
      return NotFound("Solicitação de show não localizada ou acesso negado.");

    if (proposta.Status != "Pending")
      return BadRequest($"Não é possível aceitar uma proposta que já está com status '{proposta.Status}'.");

    // Altera o status para Pré-Aprovado (Trava a vitrine e aguarda pagamento)
    proposta.Status = "Pre_Approved";

    await _context.SaveChangesAsync();
    return Ok(new { message = "Show aceito com sucesso! Proposta enviada para a esteira de pagamento do contratante." });
  }

  // ====================================================================
  // 5.8. POST: Músico Recusa Proposta de Show (Muda para Rejected)
  // ====================================================================
  // POST: api/tenants/agenda/requests/{id}/reject
  // ====================================================================
  // 5.8. POST: MÚSICO RECUSA DE VEZ (Muda para Rejected e libera calendário)
  // ====================================================================
  // POST: api/tenants/agenda/requests/{id:guid}/reject
  [HttpPost("requests/{id:guid}/reject")]
  public async Task<IActionResult> RejectShowRequest(Guid id, [FromBody] ActionWithNotesRequest request)
  {
    var tenantIdLogado = ObterTenantIdLogado();

    var proposta = await _context.ArtistEvents
        .FirstOrDefaultAsync(e => e.Id == id && e.UserId == tenantIdLogado);

    if (proposta == null)
      return NotFound("Solicitação de show não localizada ou acesso negado.");

    // Permite recusar se estiver Pendente ou em Negociação prévia
    if (proposta.Status != "Pending" && proposta.Status != "In_Negotiation")
      return BadRequest($"Não é possível recusar uma proposta com status '{proposta.Status}'.");

    // Transiciona o status e armazena a justificativa de descarte enviada pela modal
    proposta.Status = "Rejected";
    proposta.Notes = !string.IsNullOrEmpty(request.Notes) ? request.Notes : "Recusado pelo artista.";

    await _context.SaveChangesAsync();
    return Ok(new { message = "Proposta recusada com sucesso. O horário foi liberado no seu calendário." });
  }

  // ====================================================================
  // 5.9. POST: ENVIAR PARA NEGOCIAÇÃO (Muda para In_Negotiation e salva contraproposta)
  // ====================================================================
  // POST: api/tenants/agenda/requests/{id:guid}/negotiate
  [HttpPost("requests/{id:guid}/negotiate")]
  public async Task<IActionResult> NegotiateShowRequest(Guid id, [FromBody] ActionWithNotesRequest request)
  {
    var tenantIdLogado = ObterTenantIdLogado();

    if (string.IsNullOrEmpty(request.Notes))
      return BadRequest("É obrigatório preencher uma mensagem ou contraproposta para iniciar a negociação.");

    var proposta = await _context.ArtistEvents
        .FirstOrDefaultAsync(e => e.Id == id && e.UserId == tenantIdLogado);

    if (proposta == null)
      return NotFound("Solicitação de show não localizada ou acesso negado.");

    if (proposta.Status != "Pending")
      return BadRequest("Apenas propostas novas 'Pendentes' podem entrar em estado de negociação.");

    // Modifica o status para "In_Negotiation" (Aba Negociando / Cor Roxa no calendário)
    proposta.Status = "In_Negotiation";
    proposta.Notes = request.Notes;

    await _context.SaveChangesAsync();
    return Ok(new { message = "Proposta movida para a mesa de negociações com sucesso!" });
  }


}
