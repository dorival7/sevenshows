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
    public async Task<IActionResult> ConnectWallet()
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users.FindAsync(tenantIdLogado);
        if (user == null) return NotFound("Músico não encontrado.");

        if (!string.IsNullOrEmpty(user.AsaasWalletId))
            return BadRequest(new { message = "Este músico já possui uma carteira conectada.", walletId = user.AsaasWalletId, link = user.AsaasOnboardingUrl });

        var address = await _context.ArtistAddresses.FirstOrDefaultAsync(a => a.UserId == tenantIdLogado);
        if (address == null) return BadRequest("O preenchimento do endereço é obrigatório.");

        try
        {
            string documento = user.PersonType == "Legal" ? user.Cnpj! : user.Cpf;
            
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
            );

            // 1. Captura o ID da carteira filha de forma segura
            user.AsaasWalletId = asaasAccount.GetProperty("id").GetString();

            // 2. CORREÇÃO BLINDADA: Tenta ler "onboardingUrl" ou "onboardingLink". Se o Asaas não enviar nenhum, evita o erro de dicionário.
            if (asaasAccount.TryGetProperty("onboardingUrl", out var urlProp))
            {
                user.AsaasOnboardingUrl = urlProp.GetString();
            }
            else if (asaasAccount.TryGetProperty("onboardingLink", out var linkProp))
            {
                user.AsaasOnboardingUrl = linkProp.GetString();
            }
            else
            {
                // Fallback de segurança para ambiente de Sandbox caso nasça aprovado direto
                user.AsaasOnboardingUrl = "https://asaas.com";
            }

            user.AsaasAccountStatus = "PENDING";

            // Salva as informações de verdade no MySQL
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Carteira digital de shows criada no Asaas com sucesso!",
                walletId = user.AsaasWalletId,
                onboardingLink = user.AsaasOnboardingUrl,
                status = user.AsaasAccountStatus
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
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
