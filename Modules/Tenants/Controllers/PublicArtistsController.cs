using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Tenants.Models;

namespace SevenShows.Api.Modules.Tenants.Controllers;

[ApiController]
[Route("api/public/artists")]
[AllowAnonymous] // 🔓 Aberto publicamente para a Landing Page e Galeria (Casting)
public class PublicArtistsController : ControllerBase
{
  private readonly AppDbContext _context;

  public PublicArtistsController(AppDbContext context)
  {
    _context = context;
  }

  [HttpGet]
  public async Task<ActionResult<IEnumerable<PublicArtistDto>>> GetCatalog([FromQuery] string? estilo, [FromQuery] string? uf)
  {
    var query = _context.Users
        .AsNoTracking() 
        .Where(u => u.ProfileStatus == "Active" && u.SubscriptionStatus == "Active") 
        .Select(u => new PublicArtistDto
        {
          Id = u.Id,
          NomeBanda = u.Name,
          Slug = u.Slug ?? string.Empty,
          EstiloMusical = u.EstiloMusical ?? "Geral",
          FormatoArtístico = u.FormatoArtístico ?? "Banda",
          Slogan = u.Slogan ?? string.Empty,

          // MANTIDO: Guarda a cidade física original para segurança do chassi
          CidadeAtendida = _context.ArtistAddresses
                .Where(a => a.UserId == u.Id)
                .Select(a => a.City)
                .FirstOrDefault() ?? "Não Informada",

          // 🆕 ADICIONADO: Extrai dinamicamente a região comercial da tabela artist_comercial_settings
          RegiaoAtendida = _context.ArtistComercialSettings
                .Where(c => c.UserId == u.Id)
                .Select(c => c.AttendedRegions)
                .FirstOrDefault() ?? "Não Informada",

          State = _context.ArtistAddresses
                .Where(a => a.UserId == u.Id)
                .Select(a => a.State)
                .FirstOrDefault() ?? string.Empty,

          FotoCapaUrl = _context.ArtistMedias
                .Where(m => m.UserId == u.Id && m.MediaType == "Cover")
                .Select(m => m.MediaUrl)
                .FirstOrDefault() ?? string.Empty,

          PrecoBase = _context.ArtistPackages
                .Where(p => p.UserId == u.Id)
                .Select(p => p.BasePrice)
                .OrderBy(p => p)
                .FirstOrDefault()
        });

    if (!string.IsNullOrEmpty(estilo))
    {
      query = query.Where(a => a.EstiloMusical.Contains(estilo));
    }

    if (!string.IsNullOrEmpty(uf))
    {
      query = query.Where(a => a.State == uf);
    }

    var resultado = await query.ToListAsync();
    return Ok(resultado);
  }

  // ====================================================================
  // 🌍 🆕 ADICIONADO: Busca Avançada por Raio de 100 km por UF (Nacional)
  // ====================================================================
  [HttpGet("search-raio")]
  public async Task<ActionResult> BuscarArtistasPorRaio([FromQuery] string cidade, [FromQuery] string uf)
  {
      if (string.IsNullOrEmpty(cidade) || string.IsNullOrEmpty(uf))
      {
          return BadRequest(new { mensagem = "Parâmetros 'cidade' e 'uf' são obrigatórios." });
      }

      string ufSanitizada = uf.ToUpper().Trim();
      string cidadeBusca = cidade.Trim();

      // 1. BUSCA INTELIGENTE NO BANCO: Usa o operador LIKE nativo do MariaDB para ignorar maiúsculas/minúsculas e acentos
      var coordenadasOrigem = await _context.CoordenadasMunicipios
          .AsNoTracking()
          .FirstOrDefaultAsync(c => c.Uf == ufSanitizada && EF.Functions.Like(c.NomeCidade, $"%{cidadeBusca}%"));

      // 💡 Contingência: Caso o contratante tenha digitado com abreviações, tenta uma varredura cruzada invertida em cache
      if (coordenadasOrigem == null)
      {
          var todasCidadesDaUf = await _context.CoordenadasMunicipios
              .AsNoTracking()
              .Where(c => c.Uf == ufSanitizada)
              .ToListAsync();

          coordenadasOrigem = todasCidadesDaUf
              .FirstOrDefault(c => c.NomeCidade.ToUpper().Contains(cidadeBusca.ToUpper()) || 
                                  cidadeBusca.ToUpper().Contains(c.NomeCidade.ToUpper()));
      }

      if (coordenadasOrigem == null)
      {
          return NotFound(new { mensagem = $"A localidade '{cidade} - {uf}' não foi localizada na base geográfica nacional." });
      }

      double latOrigem = (double)coordenadasOrigem.Latitude;
      double lngOrigem = (double)coordenadasOrigem.Longitude;

      // ====================================================================
      // 🚀 2. CONTINUAÇÃO DO CHASSI DA QUERY DE ARTISTAS DA UF (MANTIDO INTACTO)
      // ====================================================================
      var artistasDaUf = await _context.Users
          .AsNoTracking()
          .Where(u => u.ProfileStatus == "Active" && u.SubscriptionStatus == "Active")
          .Select(u => new
          {
              Id = u.Id,
              NomeBanda = u.Name,
              Slug = u.Slug ?? string.Empty,
              EstiloMusical = u.EstiloMusical ?? "Geral",
              FormatoArtístico = u.FormatoArtístico ?? "Banda",
              Slogan = u.Slogan ?? string.Empty,

              CidadeAtendida = _context.ArtistAddresses
                  .Where(a => a.UserId == u.Id)
                  .Select(a => a.City)
                  .FirstOrDefault() ?? "Não Informada",

              State = _context.ArtistAddresses
                  .Where(a => a.UserId == u.Id)
                  .Select(a => a.State)
                  .FirstOrDefault() ?? string.Empty,

              FotoCapaUrl = _context.ArtistMedias
                  .Where(m => m.UserId == u.Id && m.MediaType == "Cover")
                  .Select(m => m.MediaUrl)
                  .FirstOrDefault() ?? string.Empty,

              PrecoBase = _context.ArtistPackages
                  .Where(p => p.UserId == u.Id)
                  .Select(p => p.BasePrice)
                  .OrderBy(p => p)
                  .FirstOrDefault()
          })
          .Where(a => a.State.ToUpper() == ufSanitizada)
          .ToListAsync();

      var coordenadasUfCache = await _context.CoordenadasMunicipios
          .AsNoTracking()
          .Where(c => c.Uf == ufSanitizada)
          .ToListAsync();

      var resultadoFinalRaio = new List<object>();

      foreach (var artista in artistasDaUf)
      {
          // Sincroniza a leitura das cidades das bandas usando casamento elástico por string contida
          var coordDestino = coordenadasUfCache
              .FirstOrDefault(c => c.NomeCidade.ToUpper().Trim() == artista.CidadeAtendida.ToUpper().Trim() || 
                                  c.NomeCidade.ToUpper().Contains(artista.CidadeAtendida.ToUpper()) ||
                                  artista.CidadeAtendida.ToUpper().Contains(c.NomeCidade.ToUpper()));

          if (coordDestino == null) continue;

          double latDestino = (double)coordDestino.Latitude;
          double lngDestino = (double)coordDestino.Longitude;

          // Fórmula de Haversine
          double R = 6371; 
          double dLat = (latDestino - latOrigem) * Math.PI / 180;
          double dLon = (lngDestino - lngOrigem) * Math.PI / 180;

          double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                     Math.Cos(latOrigem * Math.PI / 180) * Math.Cos(latDestino * Math.PI / 180) *
                     Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

          double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
          double distanciaKm = R * c;

          if (distanciaKm <= 100)
          {
              // Busca rápida da região atendida para exibição textual no card
              string regiaoComercial = _context.ArtistComercialSettings
                  .Where(c => c.UserId == artista.Id)
                  .Select(c => c.AttendedRegions)
                  .FirstOrDefault() ?? artista.CidadeAtendida;

              resultadoFinalRaio.Add(new
              {
                  artista.Id,
                  artista.NomeBanda,
                  artista.Slug,
                  artista.EstiloMusical,
                  artista.FormatoArtístico,
                  artista.Slogan,
                  artista.CidadeAtendida, // 🚀 MANTIDO: Usado pelo motor de rotas
                  RegiaoAtendida = regiaoComercial, // 🆕 ADICIONADO: Usado puramente para o texto do card
                  artista.State,
                  artista.FotoCapaUrl,
                  artista.PrecoBase,
                  DistanciaCalculada = Math.Round(distanciaKm, 1)
              });
          }
      }

      return Ok(resultadoFinalRaio);
  }

  // 🆕 ADICIONADO: Endpoint Público para carregar os detalhes do EPK e os pacotes pelo Slug da URL
  [HttpGet("{slug}")]
  public async Task<ActionResult> GetArtistBySlug([FromRoute] string slug)
  {
    if (string.IsNullOrWhiteSpace(slug))
    {
      return BadRequest(new { mensagem = "O slug do artista é obrigatório." });
    }

    var artista = await _context.Users
        .AsNoTracking()
        .Where(u => u.Slug == slug.Trim().ToLower() && u.ProfileStatus == "Active")
        .Select(u => new
        {
          Id = u.Id,
          NomeBanda = u.Name,
          Slug = u.Slug,
          EstiloMusical = u.EstiloMusical ?? "Geral",
          FormatoArtístico = u.FormatoArtístico ?? "Banda",
          Slogan = u.Slogan ?? string.Empty,
          Biografia = u.Biografia ?? string.Empty,

          CidadeAtendida = _context.ArtistAddresses
                .Where(a => a.UserId == u.Id)
                .Select(a => a.City)
                .FirstOrDefault() ?? "Não Informada",

          State = _context.ArtistAddresses
                .Where(a => a.UserId == u.Id)
                .Select(a => a.State)
                .FirstOrDefault() ?? string.Empty,

          FotoCapaUrl = _context.ArtistMedias
                .Where(m => m.UserId == u.Id && (m.MediaType == "Cover" || m.MediaType == "cover"))
                .Select(m => m.MediaUrl)
                .FirstOrDefault() ?? string.Empty,

          // ⚡ PACOTES COMERCIAIS PARA CONTRATAÇÃO DIRETA
          Pacotes = _context.ArtistPackages
                .Where(p => p.UserId == u.Id)
                .Select(p => new
                {
                  Id = p.Id,
                  Title = p.Title,
                  Description = p.Description,
                  DurationMinutes = p.DurationMinutes,
                  BasePrice = p.BasePrice
                })
                .ToList(),

          // 🎬📸 TODAS AS MÍDIAS DO PORTFÓLIO (O Front-end se encarrega de separar Fotos e Vídeos)
          Medias = _context.ArtistMedias
                .Where(m => m.UserId == u.Id)
                .Select(m => new
                {
                  Id = m.Id,
                  MediaUrl = m.MediaUrl,
                  MediaType = m.MediaType // Devolve o tipo original gravado (Photo, Video, cover, etc)
                })
                .ToList()
        })
        .FirstOrDefaultAsync();

    if (artista == null)
    {
      return NotFound(new { mensagem = "Artista não localizado no banco de dados." });
    }

    return Ok(artista);
  }

  // ====================================================================
  // 🏛️ GET: Detalhes Unificados do Pacote e da Banda para o Pré-Checkout
  // ====================================================================
  [HttpGet("/api/public/packages/{pacoteId:guid}")]
  public async Task<ActionResult> GetPublicPackageDetail([FromRoute] Guid pacoteId)
  {
    // 🚀 PROJEÇÃO AVANÇADA: Cruza as tabelas mapeadas no EF Core de forma otimizada com subqueries
    var pacoteDetalhado = await _context.ArtistPackages
        .AsNoTracking()
        .Where(p => p.Id == pacoteId)
        .Select(p => new
        {
          IdPackage = p.Id,
          TituloPacote = p.Title,
          DescricaoPacote = p.Description ?? string.Empty,
          DuracaoMinutos = p.DurationMinutes,
          PrecoBase = p.BasePrice,

          UserId = p.UserId,
          NomeBanda = _context.Users.Where(u => u.Id == p.UserId).Select(u => u.Name).FirstOrDefault() ?? "Atração Sem Nome",
          EstiloMusical = _context.Users.Where(u => u.Id == p.UserId).Select(u => u.EstiloMusical).FirstOrDefault() ?? "Geral",
          FormatoArtistico = _context.Users.Where(u => u.Id == p.UserId).Select(u => u.FormatoArtístico).FirstOrDefault() ?? "Banda",

          // 🗺️ LOGÍSTICA DE DISLOCAMENTO: Extrai reativamente da tabela 'artist_addresses'
          CidadeOrigem = _context.ArtistAddresses
                .Where(a => a.UserId == p.UserId)
                .Select(a => a.City)
                .FirstOrDefault() ?? "Não Informada",

          EstadoOrigem = _context.ArtistAddresses
                .Where(a => a.UserId == p.UserId)
                .Select(a => a.State)
                .FirstOrDefault() ?? string.Empty,

          ZipCodeOrigem = _context.ArtistAddresses
                .Where(a => a.UserId == p.UserId)
                .Select(a => a.ZipCode)
                .FirstOrDefault() ?? string.Empty,

          // 🚚 REGRAS COMERCIAIS DE FRETE: Extrai reativamente da tabela 'artist_comercial_settings'
          FreeRadiusKm = _context.ArtistComercialSettings
                .Where(c => c.UserId == p.UserId)
                .Select(c => c.FreeRadiusKm)
                .FirstOrDefault(),

          ExtraKmValue = _context.ArtistComercialSettings
                .Where(c => c.UserId == p.UserId)
                .Select(c => c.ExtraKmValue)
                .FirstOrDefault()
        })
        .FirstOrDefaultAsync();

    if (pacoteDetalhado == null)
    {
      return NotFound(new { mensagem = "O formato de show selecionado não foi localizado no portal." });
    }

    return Ok(pacoteDetalhado);
  }

  // ====================================================================
  // 🏛️ GET: Consulta o Mapa de Calor de Disponibilidade Ajustado ao Banco
  // ====================================================================
  [HttpGet("/api/public/artists/{artistId:guid}/availability")]
  public async Task<ActionResult> GetArtistAvailability(
      [FromRoute] Guid artistId, 
      [FromQuery] int mes, 
      [FromQuery] int ano, 
      [FromQuery] Guid? contractorId) // 🚀 INJEÇÃO DO FRONT: Recebe o ID do contratante logado de forma opcional
  {
    if (mes < 1 || mes > 12 || ano < 2026)
    {
        return BadRequest(new { mensagem = "Parâmetros de mês ou ano inválidos." });
    }

    var dataInicio = new DateTime(ano, mes, 1);
    var dataFim = dataInicio.AddMonths(1).AddDays(-1);

    // 1. BUSCA A GRADE SEMANAL COM O SEU RESPECTIVO HORÁRIO DE INÍCIO
    var gradeSemanal = await _context.ArtistAvailabilities
        .AsNoTracking()
        .Where(a => a.UserId == artistId && a.IsAvailable)
        .Select(a => new { a.DayOfWeek, a.StartTime })
        .ToListAsync();

    var diasDisponiveisSemana = gradeSemanal.Select(a => a.DayOfWeek).ToList();

    // 2. BUSCA OS RANGES DE RECESSOS / FERIADOS
    var bloqueiosAgenda = await _context.ArtistAgendaBlocks
        .AsNoTracking()
        .Where(b => b.UserId == artistId && 
                    ((b.StartDate >= dataInicio && b.StartDate <= dataFim) || 
                    (b.EndDate >= dataInicio && b.EndDate <= dataFim)))
        .Select(b => new { b.StartDate, b.EndDate })
        .ToListAsync();

    // 3. BUSCA OS SHOWS DO MÊS
    var showsConfirmados = await _context.ArtistEvents
        .AsNoTracking()
        .Where(e => e.UserId == artistId && e.EventDate >= dataInicio && e.EventDate <= dataFim && 
                    (e.Status == "Confirmed" || e.Status == "Pending" || e.Status == "Pre_Approved"))
        .Select(e => new { e.EventDate.Day, e.ContractorId })
        .ToListAsync();

    var diasDoMes = new List<object>();
    var totalDias = DateTime.DaysInMonth(ano, mes);

    for (int dia = 1; dia <= totalDias; dia++)
    {
        var dataCorrente = new DateTime(ano, mes, dia);
        int diaDaSemanaInt = (int)dataCorrente.DayOfWeek;
        
        string status = "folga";
        string startTimeFormatado = null;

        var estaBloqueado = bloqueiosAgenda.Any(b => 
            dataCorrente.Date >= b.StartDate.Date && 
            (b.EndDate.Year == 1 || dataCorrente.Date <= b.EndDate.Date));

        var showNoDia = showsConfirmados.FirstOrDefault(s => s.Day == dia);

        if (showNoDia != null)
        {
            // 🟡 "sua-reserva" (Amarelo) se bater com o contractorId enviado pelo front, senão 🔵 "reservado" (Azul)
            status = (contractorId.HasValue && showNoDia.ContractorId == contractorId.Value) ? "sua-reserva" : "reservado";
        }
        else if (estaBloqueado)
        {
            status = "recesso"; // 🔴 Vermelho Danger
        }
        else if (diasDisponiveisSemana.Contains(diaDaSemanaInt))
        {
            status = "disponivel"; // 🌑 Cinza Escuro
            
            var configDia = gradeSemanal.FirstOrDefault(g => g.DayOfWeek == diaDaSemanaInt);
            if (configDia != null)
            {
                startTimeFormatado = configDia.StartTime.ToString(@"hh\:mm");
            }
        }

        diasDoMes.Add(new { dia = dia, status = status, startTime = startTimeFormatado });
    }

    return Ok(new { Ano = ano, Mes = mes, Dias = diasDoMes });
  }

  // ====================================================================
  // 🌍 🆕 ADICIONADO: Endpoint de Autocomplete Geográfico Nacional Ultra-Leve
  // ====================================================================
  [HttpGet("cities/autocomplete")] // 🛠️ O .NET unirá reativamente com api/public/artists resultando em api/public/artists/cities/autocomplete
  public async Task<ActionResult<List<string>>> GetCitiesAutocomplete([FromQuery] string termo)
  {
      if (string.IsNullOrEmpty(termo) || termo.Trim().Length < 3)
      {
          return Ok(new List<string>());
      }

      string termoBusca = termo.Trim();

      var cidadesFiltradas = await _context.CoordenadasMunicipios
          .AsNoTracking()
          .Where(c => EF.Functions.Like(c.NomeCidade, $"%{termoBusca}%"))
          .OrderBy(c => c.NomeCidade)
          .Select(c => $"{c.NomeCidade} - {c.Uf}")
          .Take(10)
          .ToListAsync();

      return Ok(cidadesFiltradas);
  }


}
