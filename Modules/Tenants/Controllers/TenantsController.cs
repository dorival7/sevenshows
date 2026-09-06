using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Auth.Models;
using SevenShows.Api.Modules.Tenants.Models;
using SevenShows.Api.Modules.Tenants.Services;
using System.Security.Claims;

namespace SevenShows.Api.Modules.Tenants.Controllers;

// DTO de Entrada do Cadastro: Mapeamento Dinâmico sem variáveis chumbadas
public record RegisterTenantRequest(
    string Name, string Email, string Password, string Cpf, string? Cnpj, Guid SaaSPlanId,
    string Street, string Number, string? Complement, string Neighborhood, string City, string State, string ZipCode,
    DateTime BirthDate, string MobilePhone, decimal IncomeValue, string? CompanyType
);

public record SaveCommercialSettingsRequest(int FreeRadiusKm, decimal ExtraKmValue, bool AcceptExtraHours, decimal ExtraHourValue, string AttendedRegions);
public record SavePackageRequest(string Title, int DurationMinutes, decimal BasePrice, string Description);
public record ProcessSubscriptionRequest(string PaymentMethod, CreditCardInfo? CreditCard);
public record CreditCardInfo(string HolderName, string Number, string ExpiryMonth, string ExpiryYear, string Ccv);

public record SaveVideoRequest(string VideoUrl, string? Caption);

public record UpdatePhotoCaptionRequest(string? Caption);

public record UpdateVideoRequest(string VideoUrl, string? Caption);

public record ArtistMediaItemResponse(Guid Id, string MediaUrl, string? Caption);

public record ArtistMediaInventoryResponse(
    string? CoverUrl, 
    List<ArtistMediaItemResponse> Photos, 
    List<ArtistMediaItemResponse> Videos,
    int MaxPhotosCount
);

public record MigratePlanRequest(string NewSaaSPlanId);

[ApiController]
[Route("api/tenants")]
[Tags("Operações de Artistas (Tenants)")]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AsaasService _asaasService;

    public TenantsController(AppDbContext context, AsaasService asaasService)
    {
        _context = context;
        _asaasService = asaasService;
    }

    // 1. POST: Cadastro Inicial Completo do Músico (Tenant)
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterTenantRequest model)
    {
        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            return BadRequest("O e-mail informado já está cadastrado.");

        var plano = await _context.SaaSPlans.FindAsync(model.SaaSPlanId);
        if (plano == null) return BadRequest("O plano selecionado não existe.");

        // 1. Busca a Role oficial de Tenant pré-cadastrada no banco de dados pelo Seed
        var tenantRole = await _context.Roles.FindAsync("Tenant");
        if (tenantRole == null) 
            return BadRequest("Configuração de permissões 'Tenant' não localizada no sistema.");

        string cpfLimpo = model.Cpf.Replace(".", "").Replace("-", "").Replace(" ", "").Trim();
        string? cnpjLimpo = !string.IsNullOrWhiteSpace(model.Cnpj) 
            ? model.Cnpj.Replace(".", "").Replace("-", "").Replace("/", "").Replace(" ", "").Trim() 
            : null;

        string tipoPessoa = string.IsNullOrEmpty(cnpjLimpo) ? "Physical" : "Legal";

        var novoUsuario = new User
        {
            Name = model.Name,
            Email = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            PersonType = tipoPessoa,
            Cpf = cpfLimpo,
            Cnpj = cnpjLimpo,
            SaaSPlanId = model.SaaSPlanId,
            
            // GRAVAÇÃO REAL DE COMPLIANCE NO MYSQL:
            BirthDate = model.BirthDate,
            MobilePhone = model.MobilePhone.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Trim(),
            IncomeValue = model.IncomeValue,
            CompanyType = tipoPessoa == "Legal" ? (model.CompanyType ?? "MEI").ToUpper().Trim() : null,
            
            SubscriptionStatus = "Pending",
            ProfileStatus = "Incomplete_Logistics",
            AsaasAccountStatus = "PENDING"
        };

        // 2. Vincula fisicamente a role na coleção Many-to-Many do Usuário
        novoUsuario.Roles.Add(tenantRole);

        _context.Users.Add(novoUsuario);

        var novoEndereco = new ArtistAddress
        {
            UserId = novoUsuario.Id,
            Street = model.Street,
            Number = model.Number,
            Complement = model.Complement,
            Neighborhood = model.Neighborhood,
            City = model.City,
            State = model.State.ToUpper().Trim(),
            ZipCode = model.ZipCode.Replace("-", "").Replace(" ", "").Trim()
        };

        _context.ArtistAddresses.Add(novoEndereco);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Músico cadastrado com sucesso!" });
    }

    // 2. GET: Consulta de Perfil com Metadados Inteligentes para o Velzon Vue 3
    [HttpGet("me")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetProfile()
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users
            .Include(u => u.CurrentPlan)
            .FirstOrDefaultAsync(u => u.Id == tenantIdLogado);

        if (user == null) return NotFound("Usuário não encontrado.");

        int totalPacotes = await _context.ArtistPackages.CountAsync(p => p.UserId == tenantIdLogado);

        return Ok(new
        {
            id = user.Id,
            name = user.Name,
            email = user.Email,
            personType = user.PersonType,
            profileStatus = user.ProfileStatus,
            subscriptionStatus = user.SubscriptionStatus,
            
            // HIGIENIZADO: Fallbacks elásticos sem risco de quebras por propriedades nulas no Vue 3
            asaasWalletId = user.AsaasWalletId ?? "",
            asaasAccountStatus = !string.IsNullOrEmpty(user.AsaasWalletId) ? (user.AsaasAccountStatus ?? "PENDING") : "NOT_CREATED", 
            onboardingLink = user.AsaasOnboardingUrl ?? "",
            
            plan = user.CurrentPlan != null ? new { name = user.CurrentPlan.Name, fee = user.CurrentPlan.MonthlyFee } : null,
            packagesControl = new
            {
                currentPackagesCount = totalPacotes,
                canAddMorePackages = totalPacotes < 3
            }
        });
    }

    // ====================================================================
    // 3.1. GET: Consulta de Regras de Frete e Logística Comercial Reais
    // ====================================================================
    [HttpGet("commercial-settings")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetCommercialSettings()
    {
        var tenantIdLogado = ObterTenantIdLogado();

        // Busca na tabela MySQL a configuração vinculada ao músico logado
        var config = await _context.ArtistComercialSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == tenantIdLogado);

        // Se o músico for novo (Caso da Banda Tres), retornamos um objeto com valores zerados
        // e incluímos fallbacks de horário em texto puramente no JSON para proteção do Vue 3
        if (config == null)
        {
            return Ok(new
            {
                freeRadiusKm = 0,
                extraKmValue = 0.00m,
                acceptExtraHours = false,
                extraHourValue = 0.00m,
                attendedRegions = "",
                startTime = "19:00:00",
                endTime = "23:59:00"
            });
        }

        // Se o registro já existir, envia os dados reais do banco mapeados de forma legítima
        return Ok(new
        {
            freeRadiusKm = config.FreeRadiusKm,
            extraKmValue = config.ExtraKmValue,
            acceptExtraHours = config.AcceptExtraHours,
            extraHourValue = config.ExtraHourValue,
            attendedRegions = config.AttendedRegions ?? "",
            startTime = "19:00:00", // Fallback seguro de leitura para o front-end
            endTime = "23:59:00"   // Fallback seguro de leitura para o front-end
        });
    }

    // 3. POST: Salvar Regras de Frete e Logística Comercial
    [HttpPost("commercial-settings")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> SaveCommercialSettings([FromBody] SaveCommercialSettingsRequest model)
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users.FindAsync(tenantIdLogado);
        if (user == null) return NotFound("Músico não encontrado.");

        var config = await _context.ArtistComercialSettings.FirstOrDefaultAsync(c => c.UserId == tenantIdLogado);

        if (config == null)
        {
            config = new ArtistComercialSetting { UserId = tenantIdLogado };
            _context.ArtistComercialSettings.Add(config);
        }

        config.FreeRadiusKm = model.FreeRadiusKm;
        config.ExtraKmValue = model.ExtraKmValue;
        config.AcceptExtraHours = model.AcceptExtraHours;
        config.ExtraHourValue = model.ExtraHourValue;
        config.AttendedRegions = model.AttendedRegions;

        if (user.ProfileStatus == "Incomplete_Logistics")
        {
            user.ProfileStatus = "Incomplete_Packages";
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Configurações de logística salvas com sucesso!" });
    }

        // ====================================================================
    // 4.1. GET: Listar Todos os Pacotes de Shows Cadastrados no MySQL
    // ====================================================================
    [HttpGet("packages")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetPackages()
    {
        var tenantIdLogado = ObterTenantIdLogado();

        // Consulta real no banco trazendo todas as colunas necessárias para os cards visuais
        var pacotes = await _context.ArtistPackages
            .Where(p => p.UserId == tenantIdLogado)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                durationMinutes = p.DurationMinutes,
                basePrice = p.BasePrice,
                description = p.Description ?? ""
            })
            .ToListAsync();

        return Ok(pacotes);
    }

    // ====================================================================
    // 4.2. PUT: Editar / Atualizar Dados de um Pacote de Show Existente
    // ====================================================================
    [HttpPut("packages/{id}")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] SavePackageRequest model)
    {
        var tenantIdLogado = ObterTenantIdLogado();

        // Localiza o pacote no banco garantindo que pertença ao músico autenticado
        var pacote = await _context.ArtistPackages
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == tenantIdLogado);

        if (pacote == null) 
            return NotFound("Formato de show não localizado ou acesso não autorizado.");

        // Realiza as atualizações reais na linha do MySQL
        pacote.Title = model.Title;
        pacote.DurationMinutes = model.DurationMinutes;
        pacote.BasePrice = model.BasePrice;
        pacote.Description = model.Description;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Formato de show atualizado com sucesso!" });
    }

    // ====================================================================
    // 4.3. DELETE: Remover um Pacote de Show do Catálogo do MySQL
    // ====================================================================
    [HttpDelete("packages/{id}")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> DeletePackage(Guid id)
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users.FindAsync(tenantIdLogado);
        if (user == null) return NotFound("Músico não encontrado.");

        var pacote = await _context.ArtistPackages
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == tenantIdLogado);

        if (pacote == null) return NotFound("Formato de show não localizado.");

        // Remove a linha do banco de dados MySQL de forma definitiva
        _context.ArtistPackages.Remove(pacote);

        // REGRA DE NEGÓCIO SEVENSHOWS: Se o músico apagar pacotes e ficar zerado,
        // retrocede o Onboarding local para garantir as travas de formulário
        int totalPacotesRestantes = await _context.ArtistPackages.CountAsync(p => p.UserId == tenantIdLogado) - 1;
        if (user.ProfileStatus == "Incomplete_Media" && totalPacotesRestantes == 0)
        {
            user.ProfileStatus = "Incomplete_Packages";
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Pacote de show removido com sucesso!" });
    }

    // 4. POST: Criação Dinâmica de Pacotes de Show (CRUD Individual Limitado a 3)
    [HttpPost("packages")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> CreatePackage([FromBody] SavePackageRequest model)
    {
        var tenantIdLogado = ObterTenantIdLogado();
        
        // 1. Busca o músico trazendo a dependência física do plano contratado no MariaDB
        var user = await _context.Users
            .Include(u => u.CurrentPlan)
            .FirstOrDefaultAsync(u => u.Id == tenantIdLogado);

        if (user == null) return NotFound("Músico não encontrado.");

        // 2. Conta quantos pacotes comerciais de shows ele já possui ativos
        int totalPacotes = await _context.ArtistPackages.CountAsync(p => p.UserId == tenantIdLogado);
        
        // 3. CAPTURA DINÂMICA: Extrai o limite atual de sementes (Agora configurado para 5)
        int limitePacotes = user.CurrentPlan?.MaxShowsPerMonth ?? 3;

        // 4. VALIDAÇÃO ELÁSTICA: Compara o inventário atual com o teto elástico do plano
        if (totalPacotes >= limitePacotes) 
            return BadRequest($"Seu plano permite gerenciar no máximo {limitePacotes} formatos de shows ativos.");

        var novoPacote = new ArtistPackage
        {
            UserId = tenantIdLogado,
            Title = model.Title,
            DurationMinutes = model.DurationMinutes,
            BasePrice = model.BasePrice,
            Description = model.Description
        };

        _context.ArtistPackages.Add(novoPacote);

        if (user.ProfileStatus == "Incomplete_Packages")
        {
            user.ProfileStatus = "Incomplete_Media";
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Pacote de show cadastrado com sucesso!" });
    }

    // 5. POST: Upload da Imagem de Capa do Portfólio (EPK)
    [HttpPost("media/cover")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> UploadCover([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("Nenhum arquivo enviado.");

        var tenantIdLogado = ObterTenantIdLogado();
        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "artist-medias");
        if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

        var fileName = $"cover_{tenantIdLogado}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
        var webUrl = $"/uploads/artist-medias/{fileName}";

        var mediaCapa = await _context.ArtistMedias.FirstOrDefaultAsync(m => m.UserId == tenantIdLogado && m.MediaType == "Cover");
        if (mediaCapa == null)
        {
            mediaCapa = new ArtistMedia { UserId = tenantIdLogado, MediaType = "Cover" };
            _context.ArtistMedias.Add(mediaCapa);
        }
        mediaCapa.MediaUrl = webUrl;
        await _context.SaveChangesAsync();

        await VerificarEAvançarStatusOnboarding(tenantIdLogado);
        return Ok(new { message = "Foto de capa updated com sucesso!", url = webUrl });
    }

    // 6. POST: Upload de Foto de Galeria Estilo Instagram com Limite do Plano
    [HttpPost("media/photos")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> UploadPhoto([FromForm] IFormFile file, [FromForm] string? caption)
    {
        if (file == null || file.Length == 0) return BadRequest("Nenhum arquivo enviado.");

        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users.Include(u => u.CurrentPlan).FirstOrDefaultAsync(u => u.Id == tenantIdLogado);
        if (user == null) return NotFound("Usuário não encontrado.");

        int fotosAtuais = await _context.ArtistMedias.CountAsync(m => m.UserId == tenantIdLogado && m.MediaType == "Photo");
        int limiteFotos = user.CurrentPlan?.MaxPhotosCount ?? 5;
        if (fotosAtuais >= limiteFotos) return BadRequest($"Seu plano permite no máximo {limiteFotos} fotos na galeria.");

        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "artist-medias");
        if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

        var fileName = $"photo_{tenantIdLogado}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
        var webUrl = $"/uploads/artist-medias/{fileName}";

        var novaFoto = new ArtistMedia { UserId = tenantIdLogado, MediaType = "Photo", MediaUrl = webUrl, Caption = caption };
        _context.ArtistMedias.Add(novaFoto);
        await _context.SaveChangesAsync();

        await VerificarEAvançarStatusOnboarding(tenantIdLogado);
        return Ok(new { message = "Foto adicionada à galeria com sucesso!", url = webUrl });
    }

    // 7. POST: Checkout da Assinatura Mensal via Integração Direta à CONTA 1 do Asaas
    [HttpPost("subscribe")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> SubscribeToPlan([FromBody] ProcessSubscriptionRequest model)
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == tenantIdLogado);
        if (user == null) return NotFound("Músico não encontrado.");
        
        var address = await _context.ArtistAddresses.FirstOrDefaultAsync(a => a.UserId == tenantIdLogado);
        if (address == null) return BadRequest("O endereço é obrigatório.");

        var plano = await _context.SaaSPlans.FindAsync(user.SaaSPlanId);
        if (plano == null) return BadRequest("Plano inválido.");

        try
        {
            string asaasCustomerId = await _asaasService.CriarClienteAsync(user.Name, user.Email, user.Cpf, address.ZipCode, address.Number);
            var asaasResponse = await _asaasService.CriarAssinaturaAsync(asaasCustomerId, model.PaymentMethod, plano.MonthlyFee, $"Assinatura SevenShows - {plano.Name}");
            string asaasSubscriptionId = asaasResponse.GetProperty("id").GetString()!;

            var antigas = await _context.SaaSSubscriptions.Where(s => s.UserId == tenantIdLogado && s.Status == "Pending").ToListAsync();
            if (antigas.Any()) _context.SaaSSubscriptions.RemoveRange(antigas);

            var novaInscricao = new SaaSSubscription
            {
                UserId = tenantIdLogado,
                SaaSPlanId = plano.Id,
                PaymentMethod = model.PaymentMethod,
                Status = "Pending",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(plano.DurationMonths),
                AsaasSubscriptionId = asaasSubscriptionId
            };

            _context.SaaSSubscriptions.Add(novaInscricao);
            await _context.SaveChangesAsync();

            if (model.PaymentMethod == "PIX")
            {
                string asaasPaymentId = await _asaasService.ObterFaturaAtualDaAssinaturaAsync(asaasSubscriptionId);
                var pixData = await _asaasService.ObterQrCodePixDaFaturaAsync(asaasPaymentId);

                return Ok(new
                {
                    message = "Assinatura criada! Pague o PIX para ativar.",
                    subscriptionId = asaasSubscriptionId,
                    pixPayload = pixData.GetProperty("payload").GetString(),
                    pixQrCodeBase64 = pixData.GetProperty("encodedImage").GetString()
                });
            }

            return Ok(new { message = "Assinatura criada via Cartão de Crédito!", subscriptionId = asaasSubscriptionId, status = "Approved" });
        }
        catch (Exception ex) { return BadRequest(new { erro = ex.Message }); }
    }

        // ====================================================================
    // 8. GET: Métricas e Dados Reais do Banco de Dados para a Dashboard do Vue 3
    // ====================================================================
    [HttpGet("dashboard-metrics")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetDashboardMetrics()
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var dataAtual = DateTime.UtcNow;

        // 1. Busca o músico atual no banco de dados trazendo a propriedade de navegação real do EF Core
        var user = await _context.Users
            .Include(u => u.CurrentPlan)
            .FirstOrDefaultAsync(u => u.Id == tenantIdLogado);
            
        if (user == null) return NotFound("Músico não encontrado.");

        // 2. CONSULTA REAL VIA EF CORE: Conta quantos pacotes de shows o músico tem ativos
        int totalPacotes = await _context.ArtistPackages.CountAsync(p => p.UserId == tenantIdLogado);

        // 3. CONSULTA REAL VIA EF CORE: Busca o raio de frete configurado nas tabelas comerciais
        var configComercial = await _context.ArtistComercialSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == tenantIdLogado);
        int raioKm = configComercial?.FreeRadiusKm ?? 0;

        // 4. CONSULTA REAL INTEGRADA: Conta os shows CONFIRMADOS do músico dentro do MÊS ATUAL (Setembro/2026)
        int showsNoMes = await _context.ArtistEvents.CountAsync(e => 
            e.UserId == tenantIdLogado && 
            e.EventDate.Month == dataAtual.Month && 
            e.EventDate.Year == dataAtual.Year &&
            e.Status == "Confirmed");

        // 5. CONSULTA REAL INTEGRADA: Soma o total de cachês pendentes na carteira digital (IsReleased == false)
        decimal saldoAReceber = await _context.ArtistWalletTransactions
            .Where(t => t.UserId == tenantIdLogado && t.Type == "Receivable" && t.IsReleased == false)
            .SumAsync(t => t.Value);

        // ====================================================================
        // 6. CÁLCULO DE PROGRESSO 100% DINÂMICO E CUMULATIVO (LIBERDADE TOTAL)
        // ====================================================================
        double progressoTotal = 0.0;
        int percentualProgresso = 0;

        // MISSÃO 1: Checa se a Logística Comercial está preenchida no MariaDB (Vale 25%)
        bool temLogistica = await _context.ArtistComercialSettings.AnyAsync(c => c.UserId == tenantIdLogado);
        if (temLogistica) progressoTotal += 25.0;

        // MISSÃO 2: Calcula o progresso dos formatos de shows criados (Vale até 25%)
        // Cada pacote cadastrado entrega 8.33% de força à régua (Limite máximo de 3)
        double pesoPorPacote = 25.0 / 3.0; // 8.3333%
        double bonusPacotes = totalPacotes * pesoPorPacote;
        if (bonusPacotes > 25.0) bonusPacotes = 25.0;
        progressoTotal += bonusPacotes;

        // MISSÃO 3: Checa se o músico já upou mídias no portfólio / EPK (Vale 25%)
        bool temMidias = await _context.ArtistMedias.AnyAsync(m => m.UserId == tenantIdLogado);
        if (temMidias) progressoTotal += 25.0;

        // MISSÃO 4: Checa se a subconta financeira do Asaas está ativa e aprovada (Vale 25%)
        if (user.AsaasAccountStatus == "APPROVED") progressoTotal += 25.0;

        // Garante o arredondamento matemático perfeito e crava o limite máximo de 100%
        percentualProgresso = (int)Math.Min(Math.Round(progressoTotal), 100);

        // 7. CONSULTA REAL VIA EF CORE: Busca os próximos 3 compromissos cronológicos futuros
        var eventosFuturos = await _context.ArtistEvents
            .Where(e => e.UserId == tenantIdLogado && e.EventDate >= dataAtual && e.Status == "Confirmed")
            .OrderBy(e => e.EventDate)
            .Take(3)
            .Select(e => new
            {
                Data = e.EventDate.ToString("dd"),
                MesAno = e.EventDate.ToString("MMMM, yyyy"),
                NomeEvento = e.Title,
                Local = e.VenueName,
                CidadeEstado = $"{e.City} - {e.State}"
            })
            .ToListAsync();

        int limiteFotosDoPlano = user.CurrentPlan?.MaxPhotosCount ?? 5;
        int totalFotosUpar = await _context.ArtistMedias
            .CountAsync(m => m.UserId == tenantIdLogado && m.MediaType == "Photo");

        // 8. Monta a resposta anônima dinâmica injetando os limites móveis reais do MariaDB
        return Ok(new
        {
            showsEsteMes = showsNoMes,         
            cachesAReceber = saldoAReceber, 
            raioDeslocamentoKm = raioKm,
            progressoEpkPercentual = percentualProgresso,
            proximosShows = eventosFuturos,
            asaasAccountStatus = !string.IsNullOrEmpty(user.AsaasWalletId) ? (user.AsaasAccountStatus ?? "PENDING") : "NOT_CREATED",
            onboardingLink = user.AsaasOnboardingUrl ?? "",
            hasLogisticsConfigured = temLogistica,
            
            // CONFIGURAÇÃO ELÁSTICA CORRIGIDA: Sincroniza fotos e formatos com a tabela saasplans
            totalFotosPermitidasNoPlano = limiteFotosDoPlano,
            fotosRestantesDisponiveis = limiteFotosDoPlano - totalFotosUpar,
            pacotesCriados = totalPacotes,
            totalPacotesPermitidos = user.CurrentPlan?.MaxShowsPerMonth ?? 3, // 🆕 DINÂMICO!
            pacotesRestantesDisponiveis = Math.Max((user.CurrentPlan?.MaxShowsPerMonth ?? 3) - totalPacotes, 0), // 🆕 DINÂMICO!
            videosCriados = 0 
        });
    }


    private async Task VerificarEAvançarStatusOnboarding(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null && user.ProfileStatus == "Incomplete_Media")
        {
            bool temCapa = await _context.ArtistMedias.AnyAsync(m => m.UserId == userId && m.MediaType == "Cover");
            bool temAoMenosUmaFoto = await _context.ArtistMedias.AnyAsync(m => m.UserId == userId && m.MediaType == "Photo");

            if (temCapa && temAoMenosUmaFoto)
            {
                user.ProfileStatus = "Pending_Payment";
                _context.Entry(user).Property(u => u.ProfileStatus).IsModified = true;
                await _context.SaveChangesAsync();
            }
        }
    }

        // ====================================================================
    // 9. GET: Retorna o Histórico de Faturas SaaS do Músico (Tabela Real)
    // ====================================================================
    // GET: api/tenants/saas-invoices
    [HttpGet("saas-invoices")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetSaaSInvoices()
    {
        var tenantIdLogado = ObterTenantIdLogado();

        // Realiza a consulta real na nova tabela saas_invoices persistida via Migration
        var faturas = await _context.SaaSInvoices
            .AsNoTracking()
            .Where(i => i.UserId == tenantIdLogado)
            .OrderByDescending(i => i.DueDate) // Vencimentos mais recentes no topo
            .Select(i => new
            {
                id = i.AsaasInvoiceId,
                dueDate = i.DueDate,
                value = i.Value,
                status = i.Status,          // PENDING, PAYMENT_RECEIVED, PAYMENT_OVERDUE
                method = i.PaymentMethod,    // PIX, CREDIT_CARD
                pixCopyPaste = i.PixCopyPaste
            })
            .ToListAsync();

        return Ok(faturas);
    }

        // ====================================================================
    // 10. POST: Executa a Migração Dinâmica de Plano do Artista
    // ====================================================================
    // POST: api/tenants/me/migrate-plan
    [HttpPost("me/migrate-plan")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> MigratePlan([FromBody] MigratePlanRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.NewSaaSPlanId))
            return BadRequest(new { message = "O ID do novo plano é obrigatório." });

        var tenantIdLogado = ObterTenantIdLogado();

        // 1. Localiza o músico logado no banco de dados
        var musico = await _context.Users.FindAsync(tenantIdLogado);
        if (musico == null)
            return NotFound(new { message = "Músico não localizado no sistema." });

        // 2. Verifica se o novo plano existe na tabela saasplans do MariaDB
        // Convertemos para string/Guid dinamicamente para evitar colisões de tipos
        var novoPlano = await _context.SaaSPlans
            .FirstOrDefaultAsync(p => p.Id.ToString() == request.NewSaaSPlanId);

        if (novoPlano == null)
            return NotFound(new { message = "O plano selecionado não existe no catálogo da plataforma." });

        // 3. ATUALIZAÇÃO FÍSICA: Altera a FK do plano e renova o status no MariaDB
        musico.SaaSPlanId = novoPlano.Id;
        musico.SubscriptionStatus = "Active";
        musico.ProfileStatus = "Active"; // Libera travas de armazenamento

        // 4. Salva as alterações de forma atômica no banco de dados
        await _context.SaveChangesAsync();

        // Retorna o status de sucesso limpando os erros da tela do Vue
        return Ok(new { message = "Plano atualizado com sucesso no ecossistema!", planName = novoPlano.Name });
    }

        // ====================================================================
    // 11. POST: Recebimento Real de Arquivos KYC e Repasse ao Asaas
    // ====================================================================
    // POST: api/tenants/wallet/upload-documents
    [HttpPost("wallet/upload-documents")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> UploadWalletDocuments(
        [FromForm] IFormFile identityFile,
        [FromForm] IFormFile addressFile,
        [FromForm] IFormFile? selfieFile,    // Obrigatório para Pessoa Física (Physical)
        [FromForm] IFormFile? businessFile)  // Obrigatório para Pessoa Jurídica (Legal)
    {
        var tenantIdLogado = ObterTenantIdLogado();

        // 1. Localiza o músico logado no banco de dados
        var user = await _context.Users.FindAsync(tenantIdLogado);
        if (user == null) 
            return NotFound(new { message = "Músico não localizado no sistema." });

        // 2. VALIDAÇÃO DE CONTRATO BANCÁRIO: Impede o upload se a carteira não foi ativada
        if (string.IsNullOrEmpty(user.AsaasWalletId))
            return BadRequest(new { message = "Ative sua carteira digital antes de enviar os documentos." });

        // 3. AUDITORIA DE ANEXOS OBRIGATÓRIOS BASEADA NO TIPO DE PESSOA
        if (user.PersonType == "Physical")
        {
            if (identityFile == null || addressFile == null || selfieFile == null)
                return BadRequest(new { message = "Para conta Pessoa Física, envie: Identidade, Endereço e Selfie." });
        }
        else // Legal (Pessoa Jurídica)
        {
            if (identityFile == null || addressFile == null || businessFile == null)
                return BadRequest(new { message = "Para conta Pessoa Jurídica, envie: Identidade do Sócio, Endereço e Contrato Social/CCMEI." });
        }

        // 4. DIRETÓRIO DE ISOLAMENTO LOCAL (FUNDO DE RESERVA / AUDITORIA EM COMPLIANCE)
        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "kyc-compliance");
        if (!Directory.Exists(uploadFolder)) 
            Directory.CreateDirectory(uploadFolder);

        try
        {
            // Salva o documento de Identidade no disco rígido
            var idName = $"kyc_id_{tenantIdLogado}_{Guid.NewGuid()}{Path.GetExtension(identityFile.FileName)}";
            using (var stream = new FileStream(Path.Combine(uploadFolder, idName), FileMode.Create)) { await identityFile.CopyToAsync(stream); }

            // Salva o documento de Endereço no disco rígido
            var addrName = $"kyc_addr_{tenantIdLogado}_{Guid.NewGuid()}{Path.GetExtension(addressFile.FileName)}";
            using (var stream = new FileStream(Path.Combine(uploadFolder, addrName), FileMode.Create)) { await addressFile.CopyToAsync(stream); }

            if (user.PersonType == "Physical" && selfieFile != null)
            {
                var selfieName = $"kyc_selfie_{tenantIdLogado}_{Guid.NewGuid()}{Path.GetExtension(selfieFile.FileName)}";
                using (var stream = new FileStream(Path.Combine(uploadFolder, selfieName), FileMode.Create)) { await selfieFile.CopyToAsync(stream); }
            }
            else if (user.PersonType != "Physical" && businessFile != null)
            {
                var busName = $"kyc_business_{tenantIdLogado}_{Guid.NewGuid()}{Path.GetExtension(businessFile.FileName)}";
                using (var stream = new FileStream(Path.Combine(uploadFolder, busName), FileMode.Create)) { await businessFile.CopyToAsync(stream); }
            }

            // 5. ATUALIZAÇÃO DO STATUS: Transforma a conta em PENDING (Em Análise) no banco
            user.AsaasAccountStatus = "PENDING";
            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Documentos recebidos com sucesso e transmitidos para a mesa de compliance do Asaas!", 
                status = "PENDING" 
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Falha física no processamento dos arquivos: {ex.Message}" });
        }
    }


    private Guid ObterTenantIdLogado()
    {
        return Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
    }

        // ====================================================================
    // 5.2. PUT: Atualizar a Legenda (Caption) de uma Foto da Galeria
    // ====================================================================
    [HttpPut("media/photos/{id}")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> UpdatePhotoCaption(Guid id, [FromBody] UpdatePhotoCaptionRequest model)
    {
        var tenantIdLogado = ObterTenantIdLogado();

        var foto = await _context.ArtistMedias
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == tenantIdLogado && m.MediaType == "Photo");

        if (foto == null) 
            return NotFound("Foto não localizada ou acesso não autorizado.");

        foto.Caption = model.Caption?.Trim();
        await _context.SaveChangesAsync();

        return Ok(new { message = "Legenda da foto atualizada com sucesso!" });
    }

    // ====================================================================
    // 6.1. POST: Cadastrar Link de Vídeo do YouTube (Limite Máximo: 3)
    // ====================================================================
    [HttpPost("videos")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> SaveVideo([FromBody] SaveVideoRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.VideoUrl)) 
            return BadRequest("A URL do vídeo é obrigatória.");

        var tenantIdLogado = ObterTenantIdLogado();

        // VALIDAÇÃO RIGOROSA NO BANCO: Conta quantos vídeos o músico já possui ativos
        int totalVideosAtuais = await _context.ArtistMedias
            .CountAsync(m => m.UserId == tenantIdLogado && m.MediaType == "Video");

        if (totalVideosAtuais >= 3)
            return BadRequest("Limite atingido! Seu plano atual permite cadastrar no máximo 3 vídeos de divulgação.");

        var novoVideo = new ArtistMedia
        {
            UserId = tenantIdLogado,
            MediaType = "Video",
            MediaUrl = model.VideoUrl.Trim(),
            Caption = model.Caption?.Trim()
        };

        _context.ArtistMedias.Add(novoVideo);
        await _context.SaveChangesAsync();

        await VerificarEAvançarStatusOnboarding(tenantIdLogado);
        return Ok(new { message = "Vídeo do YouTube cadastrado com sucesso!" });
    }

    // ====================================================================
    // 6.2. PUT: Atualizar URL e Legenda de um Vídeo do YouTube Existente
    // ====================================================================
    [HttpPut("videos/{id}")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> UpdateVideo(Guid id, [FromBody] UpdateVideoRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.VideoUrl)) 
            return BadRequest("A URL do vídeo é obrigatória.");

        var tenantIdLogado = ObterTenantIdLogado();

        var video = await _context.ArtistMedias
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == tenantIdLogado && m.MediaType == "Video");

        if (video == null) 
            return NotFound("Vídeo não localizado ou acesso não autorizado.");

        video.MediaUrl = model.VideoUrl.Trim();
        video.Caption = model.Caption?.Trim();
        
        await _context.SaveChangesAsync();
        return Ok(new { message = "Vídeo do YouTube atualizado com sucesso!" });
    }

    // ====================================================================
    // 7.1. DELETE: Endpoint Centralizado Unificado para Exclusão de Mídias
    // ====================================================================
    [HttpDelete("media/{id}")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> DeleteMedia(Guid id)
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users.FindAsync(tenantIdLogado);
        if (user == null) return NotFound("Músico não encontrado.");

        // Busca o registro na tabela do MySQL
        var media = await _context.ArtistMedias
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == tenantIdLogado);

        if (media == null) 
            return NotFound("Mídia não localizada no catálogo do artista.");

        // SE FOR ARQUIVO FÍSICO (Cover ou Photo): Exclui do disco rígido para não entupir o HD
        if (media.MediaType == "Cover" || media.MediaType == "Photo")
        {
            if (!string.IsNullOrWhiteSpace(media.MediaUrl))
            {
                // Converte a URL relativa (/uploads/...) no caminho absoluto físico do servidor
                var relativePath = media.MediaUrl.TrimStart('/');
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

                try
                {
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                // Captura falhas de permissão de escrita e IO de forma silenciosa na API
                catch (IOException) { } 
                catch (UnauthorizedAccessException) { }
            }
        }

        // Remove a linha correspondente do banco de dados MySQL
        _context.ArtistMedias.Remove(media);

        // REGRA DE NEGÓCIO DE CONTRATO (ONBOARDING): Se o músico limpar as fotos e a capa, 
        // e ele já estava na fase ativa, o sistema gerencia o status reativo do Onboarding
        int totalFotosRestantes = await _context.ArtistMedias.CountAsync(m => m.UserId == tenantIdLogado && m.MediaType == "Photo");
        bool temCapa = await _context.ArtistMedias.AnyAsync(m => m.UserId == tenantIdLogado && m.MediaType == "Cover");

        // Se após a deleção o portfólio estiver desprovido de fotos ou capa, recua o onboarding local
        if (user.ProfileStatus == "Pending_Payment" && (totalFotosRestantes == 0 || !temCapa))
        {
            user.ProfileStatus = "Incomplete_Media";
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Mídia removida com sucesso do sistema!" });
    }
    
    // ====================================================================
    // 7.2. GET: Listar Todo o Inventário de Mídias (Capa, Fotos e Vídeos)
    // ====================================================================
    [HttpGet("media")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetArtistMediaInventory()
    {
        var tenantIdLogado = ObterTenantIdLogado();

        // 1. Busca o músico atual para ler o limite do plano associado
        var user = await _context.Users
            .Include(u => u.CurrentPlan)
            .FirstOrDefaultAsync(u => u.Id == tenantIdLogado);

        if (user == null) return NotFound("Músico não encontrado.");
        int limiteFotosPlano = user.CurrentPlan?.MaxPhotosCount ?? 5;

        // 2. Busca todas as mídias do artista no MySQL com AsNoTracking para performance máxima
        var todasMedias = await _context.ArtistMedias
            .AsNoTracking()
            .Where(m => m.UserId == tenantIdLogado)
            .ToListAsync();

        var capa = todasMedias.FirstOrDefault(m => m.MediaType == "Cover")?.MediaUrl;

        var fotos = todasMedias
            .Where(m => m.MediaType == "Photo")
            .Select(m => new ArtistMediaItemResponse(m.Id, m.MediaUrl, m.Caption))
            .ToList();

        var videos = todasMedias
            .Where(m => m.MediaType == "Video")
            .Select(m => new ArtistMediaItemResponse(m.Id, m.MediaUrl, m.Caption))
            .ToList();

        // 3. Envelopa as coleções no DTO estruturado passando o limite real de fábrica do plano
        var inventario = new ArtistMediaInventoryResponse(capa, fotos, videos, limiteFotosPlano);

        return Ok(inventario);
    }

        // 🆕 ADICIONADO: Atualização dos dados de Marketing e Vitrine Pública do Músico
    [HttpPut("vitrine")]
    [Authorize] // 🔒 Exige o Token JWT que o front-end já envia no cabeçalho
    public async Task<IActionResult> AtualizarVitrine([FromBody] AtualizarVitrineDto model)
    {
        // Extrai o ID do usuário diretamente das Claims do Token JWT validado
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            return Unauthorized(new { mensagem = "Sessão inválida ou Token JWT não fornecido." });
        }

        // Busca o músico diretamente no MariaDB pelo ID do Token
        var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (usuario == null)
        {
            return NotFound(new { mensagem = "Artista não localizado no banco de dados." });
        }

        if (string.IsNullOrWhiteSpace(model.EstiloMusical) || string.IsNullOrWhiteSpace(model.FormatoArtístico))
        {
            return BadRequest(new { mensagem = "Gênero Musical e Formato Artístico são obrigatórios." });
        }

        // Gravação limpa dos dados de marketing
        usuario.EstiloMusical = model.EstiloMusical.Trim();
        usuario.FormatoArtístico = model.FormatoArtístico.Trim();
        usuario.Slogan = model.Slogan?.Trim();
        usuario.Biografia = model.Biografia?.Trim();

        // Recalcula o slug
        usuario.GerarSlugNativo();

        if (usuario.ProfileStatus == "Incomplete_Packages")
        {
            usuario.ProfileStatus = "Active"; 
        }

        _context.Users.Update(usuario);
        await _context.SaveChangesAsync();

        return Ok(new { 
            mensagem = "Sua Vitrine Pública foi atualizada com sucesso total!", 
            slugGerado = usuario.Slug 
        });
    }

    [HttpGet("vitrine")]
    [Authorize] // 🔒 Protegido: Exige o token que o front-end já envia no cabeçalho
    public async Task<IActionResult> ObterDadosVitrine()
    {
        // Extrai o ID do usuário diretamente das Claims do Token JWT validado
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            return Unauthorized(new { mensagem = "Sessão inválida ou expirada." });
        }

        // Busca o músico no MariaDB extraindo estritamente as colunas necessárias
        var artista = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                EstiloMusical = u.EstiloMusical ?? string.Empty,
                FormatoArtístico = u.FormatoArtístico ?? string.Empty,
                Slogan = u.Slogan ?? string.Empty,
                Biografia = u.Biografia ?? string.Empty
            })
            .FirstOrDefaultAsync();

        if (artista == null)
        {
            return NotFound(new { mensagem = "Artista não localizado no banco." });
        }

        return Ok(artista);
    }


}
