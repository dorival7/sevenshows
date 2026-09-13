using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Tenants.Services;
using System.Security.Claims;

namespace SevenShows.Api.Modules.Tenants.Controllers;

[ApiController]
[Route("api/tenants/wallet")]
[Authorize(Roles = "Tenant")]
[Tags("Carteira de Recebimento de Shows do Artista")]
public class WalletController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AsaasWalletService _walletService;

    public WalletController(AppDbContext context, AsaasWalletService walletService)
    {
        _context = context;
        _walletService = walletService;
    }

    // 📋 DTO de Entrada: Captura os dados de faturamento parametrizados no painel privado
    public record ConnectWalletRequest(
        string PersonType, 
        string? Cnpj, 
        string? CompanyType, 
        decimal IncomeValue
    );

    // ====================================================================
    // 4. GET: Retorna o Histórico de Transações e Extrato Real do MySQL
    // ====================================================================
    // GET: api/tenants/wallet/transactions
    [HttpGet("transactions")]
    public async Task<IActionResult> GetWalletTransactions()
    {
        var tenantIdLogado = ObterTenantIdLogado();

        // Realiza a varredura real na tabela persistida no MariaDB
        var transacoes = await _context.ArtistWalletTransactions
            .AsNoTracking()
            .Where(t => t.UserId == tenantIdLogado)
            .OrderByDescending(t => t.CreatedAt) // Mais recentes no topo do extrato
            .Select(t => new
            {
                id = t.Id,
                type = t.Type,       // Receivable ou Payout
                value = t.Value,     // Valor monetário decimal
                isReleased = t.IsReleased,
                createdAt = t.CreatedAt
            })
            .ToListAsync();

        return Ok(transacoes);
    }

    [HttpPost("connect")]
    public async Task<IActionResult> ConnectWallet([FromBody] ConnectWalletRequest request)
    {
        var tenantIdLogado = ObterTenantIdLogado(); //
        var user = await _context.Users.FindAsync(tenantIdLogado); //
        if (user == null) return NotFound("Músico não encontrado."); //

        // ====================================================================
        // 💾 1. PERSISTÊNCIA ATÔMICA DOS DADOS COMERCIAIS NO BANCO DE DADOS
        // ====================================================================
        user.PersonType = request.PersonType.Trim(); //
        user.IncomeValue = request.IncomeValue; //

        if (request.PersonType == "Legal") //
        {
            user.Cnpj = request.Cnpj?.Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "").Trim(); //
            user.CompanyType = request.CompanyType?.ToUpper().Trim(); //
        }
        else
        {
            user.Cnpj = null; //
            user.CompanyType = null; //
        }

        await _context.SaveChangesAsync(); //
        
        // 🔍 LOG 1: Valida a gravação local e imprime os parâmetros recebidos do front-end
        Console.WriteLine($"🎰 [AUDITORIA LOCAL] Banco atualizado para Usuário ID: {user.Id}");
        Console.WriteLine($"   -> Nome Músico: {user.Name} | Email: {user.Email}");
        Console.WriteLine($"   -> PersonType: {request.PersonType} | IncomeValue: {request.IncomeValue}");
        Console.WriteLine($"   -> CNPJ Sanitizado: {user.Cnpj} | CompanyType: {user.CompanyType}");

        // ====================================================================
        // 🛡️ 2. TRATAMENTOS E TRAVAS DE SEGURANÇA E DEPENDÊNCIA ORIGINAIS
        // ====================================================================
        if (!string.IsNullOrEmpty(user.AsaasWalletId)) //
            return BadRequest(new { message = "Este músico já possui uma carteira conectada.", walletId = user.AsaasWalletId, link = user.AsaasOnboardingUrl }); //

        var address = await _context.ArtistAddresses.FirstOrDefaultAsync(a => a.UserId == tenantIdLogado); //
        if (address == null) return BadRequest("O preenchimento do endereço é obrigatório."); //

        // 🔍 LOG 2: Imprime os dados postais que serão injetados na requisição externa
        Console.WriteLine($"🏡 [AUDITORIA POSTAL] Endereço localizado para cruzamento:");
        Console.WriteLine($"   -> CEP: {address.ZipCode} | Número: {address.Number} | Rua: {address.Street}");

        // ====================================================================
        // 🚀 3. MONTAGEM DE PAYLOAD E FILIAÇÃO NO GATEWAY DE RECEBIMENTOS
        // ====================================================================
        try
        {
            string documento = user.PersonType == "Legal" ? user.Cnpj! : user.Cpf; //
            
            // 🔍 LOG 3: Alerta o momento exato do disparo com os dados processados
            Console.WriteLine($"📡 [DISPARO ASAAS] Invocando CriarSubcontaParceiroAsync...");
            Console.WriteLine($"   -> Documento Final Utilizado: {documento}");

            var asaasAccount = await _walletService.CriarSubcontaParceiroAsync(
                user.Name, 
                user.Email, 
                documento, 
                user.PersonType, 
                address.ZipCode, 
                address.Number,
                user.BirthDate,
                user.Cpf,
                user.MobilePhone,
                user.IncomeValue,
                user.CompanyType
            ); //

            user.AsaasWalletId = asaasAccount.GetProperty("id").GetString(); //

            if (asaasAccount.TryGetProperty("onboardingUrl", out var urlProp)) //
            {
                user.AsaasOnboardingUrl = urlProp.GetString(); //
            }
            else if (asaasAccount.TryGetProperty("onboardingLink", out var linkProp)) //
            {
                user.AsaasOnboardingUrl = linkProp.GetString(); //
            }
            else
            {
                user.AsaasOnboardingUrl = "https://asaas.com"; //
            }

            user.AsaasAccountStatus = "PENDING"; //
            await _context.SaveChangesAsync(); //

            Console.WriteLine($"✅ [SUCESSO TOTAL] Subconta autorizada pelo gateway! ID: {user.AsaasWalletId}"); //

            return Ok(new
            {
                message = "Carteira digital de shows criada com sucesso!",
                walletId = user.AsaasWalletId,
                onboardingLink = user.AsaasOnboardingUrl,
                status = user.AsaasAccountStatus
            }); //
        }
        catch (Exception ex)
        {
            // ====================================================================
            // 🚨 CAPTURA DETALHADA DO VERDADEIRO ERRO (DIAGNÓSTICO DA REJEIÇÃO)
            // ====================================================================
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("💥 ==================== EXCEÇÃO DETECTADA NO WEB SERVICE ====================");
            Console.WriteLine($"❌ Tipo da Exceção: {ex.GetType().FullName}");
            Console.WriteLine($"❌ Mensagem do Erro: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"❌ Causa Interna (InnerException): {ex.InnerException.Message}");
            }
            
            Console.WriteLine($"❌ StackTrace simplificado: {ex.StackTrace?.Split('\n')[0]}");
            Console.ResetColor();

            // Devolve o detalhe técnico real na propriedade erro para inspeção no console do front-end
            return BadRequest(new { 
                erro = ex.Message, 
                detalhe = ex.InnerException?.Message ?? "Verifique os logs detalhados vermelhos no terminal do C#." 
            });
        }
    }

    [HttpPost("simulate-kyc-approval")]
    public async Task<IActionResult> SimulateKycApproval()
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users.FindAsync(tenantIdLogado);
        if (user == null) return NotFound("Músico não encontrado.");

        if (string.IsNullOrEmpty(user.AsaasWalletId))
            return BadRequest("A carteira precisa ser inicializada primeiro.");

        user.AsaasAccountStatus = "APPROVED";
        await _context.SaveChangesAsync();

        return Ok(new { message = "SIMULAÇÃO: Documentos aprovados! O banner visual do Velzon foi removido de forma definitiva." });
    }

    private Guid ObterTenantIdLogado()
    {
        return Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
    }
}
