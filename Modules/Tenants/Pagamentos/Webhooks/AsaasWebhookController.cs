using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using System.Text.Json;

namespace SevenShows.Api.Modules.Tenants.Pagamentos.Webhooks
{
    [ApiController]
    [Route("api/public/webhooks/asaas")]
    [AllowAnonymous] // 🚀 OBRIGATÓRIO: Permite que os servidores do Asaas enviem o POST de notificação
    public class AsaasWebhookController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AsaasWebhookController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> ReceberNotificacao([FromBody] JsonElement request)
        {
            try
            {
                if (!request.TryGetProperty("event", out var eventProperty))
                    return BadRequest(new { message = "Evento não informado no payload." });

                string tipoEvento = eventProperty.GetString() ?? string.Empty;

                // Intercepta o evento legítimo de liquidação enviado pelo Asaas
                if (tipoEvento.ToUpper() == "PAYMENT_RECEIVED")
                {
                    var paymentObj = request.GetProperty("payment");
                    string asaasPaymentId = paymentObj.GetProperty("id").GetString() ?? string.Empty;

                    // Localiza o show exato comparando o ID da fatura na coluna do banco
                    var eventoShow = await _context.ArtistEvents
                        .FirstOrDefaultAsync(e => e.AsaasPaymentId == asaasPaymentId);

                    if (eventoShow != null)
                    {
                        // 🔒 TRAVA DE AGENDA: Sintoniza o status correto exigido pelo motor do calendário!
                        eventoShow.Status = "Confirmed";

                        decimal valorBruto = eventoShow.TotalProposedPrice;
                        decimal valorLiquidoMusico = Math.Round(valorBruto - (valorBruto * 0.05m), 2);

                        var transacaoCarteira = new
                        {
                            Id = Guid.NewGuid(),
                            UserId = eventoShow.UserId,
                            Type = "Receivable",
                            Value = valorLiquidoMusico,
                            IsReleased = 0, // Retido em custódia até a realização do evento
                            CreatedAt = DateTime.UtcNow
                        };

                        await _context.Database.ExecuteSqlRawAsync(
                            "INSERT INTO artistwallettransactions (Id, UserId, Type, Value, IsReleased, CreatedAt) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                            transacaoCarteira.Id, transacaoCarteira.UserId, transacaoCarteira.Type, transacaoCarteira.Value, transacaoCarteira.IsReleased, transacaoCarteira.CreatedAt
                        );

                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new { success = true, message = "Gatilho de liquidação processado com sucesso absoluto!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Falha interna.", error = ex.Message });
            }
        }
    }
}
