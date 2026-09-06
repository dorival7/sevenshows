using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Modules.Tenants.Models;
using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SevenShows.Api.Modules.Tenants.Controllers;

// DTOs DE ENTRADA DO WEBHOOK EXPANDIDOS COM GOVERNANÇA PARA PROVER INTEL_IGÊNCIA FINANCEIRA
public record AsaasWebhookPayload(
    string @event, 
    WebhookSubscriptionData? subscription,
    WebhookPaymentData? payment
);

public record WebhookSubscriptionData(string id, string customer, string status);

public record WebhookPaymentData(
    string id, 
    string subscriptionId, 
    decimal value, 
    string status, 
    string paymentMethod, 
    string? pixCopyPaste, 
    DateTime dueDate
);

[ApiController]
[Route("api/webhooks")]
[Tags("Webhook Oficial de Integração Financeira (Asaas SaaS)")]
public class SaaSWebhooksController : ControllerBase
{
    private readonly AppDbContext _context;

    public SaaSWebhooksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("asaas-saas")]
    [AllowAnonymous] // Aberto a chamadas externas para o Asaas conseguir notificar na nuvem
    public async Task<IActionResult> ReceiveAsaasNotification([FromBody] AsaasWebhookPayload payload)
    {
        if (payload == null)
            return BadRequest("Payload vazio ou inválido.");

        // O Asaas pode enviar o ID da assinatura no nó principal ou aninhado dentro do payment
        string? subscriptionId = payload.subscription?.id ?? payload.payment?.subscriptionId;

        // IGNORA SE FOR UM EVENTO DE CONTA QUE NÃO POSSUI ASSINATURA ANEXA
        if (string.IsNullOrEmpty(subscriptionId) && payload.@event != "ACCOUNT_STATUS_UPDATED")
            return BadRequest("ID da assinatura não localizado no payload.");

        // Busca o contrato mestre de recorrência do músico no MariaDB
        var assinatura = !string.IsNullOrEmpty(subscriptionId)
            ? await _context.SaaSSubscriptions.FirstOrDefaultAsync(s => s.AsaasSubscriptionId == subscriptionId)
            : null;

        Guid? userId = assinatura?.UserId;

        switch (payload.@event)
        {
            // ====================================================================
            // MOMENTO 2: GERAÇÃO AUTÔNOMA DE NOVA MENSALIDADE EM RECORRÊNCIA
            // ====================================================================
            case "PAYMENT_CREATED":
                if (payload.payment == null || userId == null) 
                    return BadRequest("Dados de faturamento ausentes.");

                // Evita duplicidade de registro se o webhook for reenviado por oscilação
                bool faturaExiste = await _context.SaaSInvoices
                    .AnyAsync(i => i.AsaasInvoiceId == payload.payment.id);

                if (!faturaExiste)
                {
                    var novaFatura = new SaaSInvoice
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId.Value,
                        AsaasInvoiceId = payload.payment.id,
                        Value = payload.payment.value,
                        Status = "PENDING", // Nasce aguardando pagamento
                        PaymentMethod = payload.payment.paymentMethod,
                        PixCopyPaste = payload.payment.pixCopyPaste,
                        DueDate = payload.payment.dueDate,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.SaaSInvoices.Add(novaFatura);
                    await _context.SaveChangesAsync();
                }
                break;

            // ====================================================================
            // ATUALIZAÇÕES DE STATUS FINANCEIRO DA MENSALIDADE
            // ====================================================================
            case "PAYMENT_RECEIVED": // Liquidação confirmada (Bateu Pix ou Cartão)
                if (assinatura != null) assinatura.Status = "Active";
                
                if (userId != null)
                {
                    var musico = await _context.Users.FindAsync(userId.Value);
                    if (musico != null)
                    {
                        musico.SubscriptionStatus = "Active";
                        if (musico.ProfileStatus == "Pending_Payment") musico.ProfileStatus = "Active";
                    }

                    // 🛡️ ATUALIZAÇÃO CIRÚRGICA: Dá a baixa física na linha da fatura específica
                    if (payload.payment != null)
                    {
                        var fatura = await _context.SaaSInvoices
                            .FirstOrDefaultAsync(i => i.AsaasInvoiceId == payload.payment.id);
                        if (fatura != null) fatura.Status = "PAYMENT_RECEIVED";
                    }
                }
                break;

            case "PAYMENT_OVERDUE": // Atraso / Cartão recusado (Inadimplência)
                if (assinatura != null) assinatura.Status = "PastDue";
                
                if (userId != null)
                {
                    var musico = await _context.Users.FindAsync(userId.Value);
                    if (musico != null)
                    {
                        musico.SubscriptionStatus = "PastDue";
                        musico.ProfileStatus = "Pending_Payment"; // Tranca o painel consultivo
                    }

                    // 🛡️ ATUALIZAÇÃO CIRÚRGICA: Transforma a fatura específica em Vencida no histórico
                    if (payload.payment != null)
                    {
                        var fatura = await _context.SaaSInvoices
                            .FirstOrDefaultAsync(i => i.AsaasInvoiceId == payload.payment.id);
                        if (fatura != null) fatura.Status = "PAYMENT_OVERDUE";
                    }
                }
                break;

            case "SUBSCRIPTION_DELETED": // Cancelamento total
                if (assinatura != null) assinatura.Status = "Canceled";
                
                if (userId != null)
                {
                    var musico = await _context.Users.FindAsync(userId.Value);
                    if (musico != null)
                    {
                        musico.SubscriptionStatus = "Canceled";
                        musico.ProfileStatus = "Incomplete_Logistics";
                    }
                }
                break;

            // ====================================================================
            // COMPLIANCE DA CARTEIRA / WALLET DO MÚSICO
            // ====================================================================
            case "ACCOUNT_STATUS_UPDATED":
                if (payload.subscription == null) return BadRequest("Dados da subconta ausentes.");

                var usuarioParceiro = await _context.Users
                    .FirstOrDefaultAsync(u => u.AsaasWalletId == payload.subscription.id); 

                if (usuarioParceiro != null)
                {
                    usuarioParceiro.AsaasAccountStatus = payload.subscription.status == "APPROVED" ? "APPROVED" : "PENDING";
                }
                break;    
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Webhook sincronizado com sucesso!" });
    }
}
