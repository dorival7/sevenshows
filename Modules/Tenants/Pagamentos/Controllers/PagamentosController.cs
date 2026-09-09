using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Tenants.Pagamentos.Dtos;
using SevenShows.Api.Modules.Tenants.Services;
using System.Text.Json;

namespace SevenShows.Api.Modules.Tenants.Pagamentos.Controllers
{
  [ApiController]
  [Route("api/public/pagamentos")]
  public class PagamentosController : ControllerBase
  {
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly AsaasService _asaasService;

    public PagamentosController(AppDbContext context, HttpClient httpClient, IConfiguration configuration, AsaasService asaasService)
    {
        _context = context;
        _httpClient = httpClient;
        _configuration = configuration;
        _asaasService = asaasService;
    }

    // ====================================================================
    // 🔒 MOTOR DE CARTÃO: Faturamento via Cartão e Trava IMEDIATA de Escrow
    // ====================================================================
    [HttpPost("processar-cartao")]
    public async Task<IActionResult> ProcessarCartao([FromBody] ProcessarPagamentoCartaoDto model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            if (!Guid.TryParse(model.ArtistEventId, out Guid artistEventGuid) || !Guid.TryParse(model.ContratanteId, out Guid contratanteGuid))
                return BadRequest(new { success = false, message = "Identificadores inválidos." });

            var eventoShow = await _context.ArtistEvents.FirstOrDefaultAsync(e => e.Id == artistEventGuid);
            var contratante = await _context.Contratantes.FirstOrDefaultAsync(c => c.Id == contratanteGuid);
            if (eventoShow == null || contratante == null) return NotFound(new { success = false, message = "Registros não localizados." });

            if (eventoShow.Status.ToLower() != "pending" && eventoShow.Status.ToLower() != "pre_approved")
                return BadRequest(new { success = false, message = "Esta proposta já foi processada." });

            var musico = await _context.Users.FirstOrDefaultAsync(u => u.Id == eventoShow.UserId);
            if (musico == null || string.IsNullOrWhiteSpace(musico.AsaasWalletId))
                return BadRequest(new { success = false, message = "Artista sem subconta ativa no Asaas." });

            var enderecoContratante = await _context.ArtistAddresses.FirstOrDefaultAsync(a => a.UserId == contratanteGuid);
            string cepSeguro = enderecoContratante?.ZipCode ?? "86430000";
            string numeroSeguro = enderecoContratante?.Number ?? "1";

            string asaasCustomerId = await _asaasService.CriarClienteAsync(
                contratante.NomeCompleto, contratante.Email, contratante.CPF, cepSeguro, numeroSeguro
            );

            var environment = _configuration["AsaasSettings:Environment"] ?? "Sandbox";
            var asaasUrl = _configuration[$"AsaasSettings:{environment}:BaseUrl"] ?? "https://asaas.com";
            var asaasToken = _configuration[$"AsaasSettings:{environment}:ApiKey"];

            decimal valorBrutoContratacao = Math.Round(eventoShow.TotalProposedPrice, 2);
            decimal taxaPlataforma = Math.Round(valorBrutoContratacao * 0.05m, 2);
            decimal valorLiquidoMusico = Math.Round(valorBrutoContratacao - taxaPlataforma, 2);

            var asaasPayload = new Dictionary<string, object>
            {
                { "customer", asaasCustomerId },
                { "billingType", "CREDIT_CARD" },
                { "value", valorBrutoContratacao },
                { "dueDate", DateTime.UtcNow.ToString("yyyy-MM-dd") },
                { "description", $"Cachê em Custódia - Cartão Proposta {eventoShow.Id}" },
                { "creditCard", new { holderName = model.HolderInfo.Name, number = model.CartaoNumero, expiryMonth = model.CartaoValidadeMes, expiryYear = model.CartaoValidadeAno, ccv = model.CartaoCvc } },
                { "creditCardHolderInfo", new { name = model.HolderInfo.Name, cpfCnpj = model.HolderInfo.CpfCnpj, email = model.HolderInfo.Email, phone = model.HolderInfo.Phone, postalCode = model.HolderInfo.PostalCode } },
                { "split", new[] { new { walletId = musico.AsaasWalletId, fixedValue = valorLiquidoMusico, chargeFee = false } } }
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("access_token", asaasToken);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SevenShowsAPI");

            var jsonOpcoes = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var jsonContent = new StringContent(JsonSerializer.Serialize(asaasPayload, jsonOpcoes), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{asaasUrl}/payments", jsonContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return BadRequest(new { success = false, message = "Recusa na transação do cartão.", erroDetalhado = responseString });

            using var jsonDoc = JsonDocument.Parse(responseString);
            var paymentId = jsonDoc.RootElement.GetProperty("id").GetString();

            // 🚀 MATEMÁTICA DA CUSTÓDIA: (Data do Show - Hoje) + Margem do AppSettings
            int diasMargem = _configuration.GetValue<int>("AsaasSettings:DiasMargemLiberacaoSplit");
            int diasAteOShow = (int)Math.Ceiling((eventoShow.EventDate.Date - DateTime.UtcNow.Date).TotalDays);
            int diasCalculados = diasAteOShow + diasMargem;
            int diasParaExpirarValido = Math.Clamp(diasCalculados, 1, 45);

            // 🔒 CADEADO REGULAMENTAR: Envia a propriedade literal "payment" para a raiz /escrow do Asaas
            var escrowPayload = new { payment = paymentId, daysToExpire = diasParaExpirarValido };
            var escrowContent = new StringContent(JsonSerializer.Serialize(escrowPayload, jsonOpcoes), System.Text.Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"{asaasUrl}/escrow", escrowContent);

            eventoShow.AsaasPaymentId = paymentId;
            eventoShow.Status = "Pending";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Pagamento efetuado com sucesso!" });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, error = ex.Message }); }
    }


      
    // ====================================================================
        // ⚡ MOTOR DE PIX: Emissão com Ativação Dinâmica do Cadeado Escrow
        // ====================================================================
      [HttpPost("gerar-pix")]
      public async Task<IActionResult> GerarPix([FromBody] EmitirPagamentoPixDto model)
      {
          if (!ModelState.IsValid) return BadRequest(ModelState);
          try
          {
              if (!Guid.TryParse(model.ArtistEventId, out Guid artistEventGuid) || !Guid.TryParse(model.ContratanteId, out Guid contratanteGuid))
                  return BadRequest(new { success = false, message = "Identificadores inválidos." });

              var eventoShow = await _context.ArtistEvents.FirstOrDefaultAsync(e => e.Id == artistEventGuid);
              var contratante = await _context.Contratantes.FirstOrDefaultAsync(c => c.Id == contratanteGuid);
              if (eventoShow == null || contratante == null) return NotFound(new { success = false, message = "Registros não localizados." });

              if (eventoShow.Status.ToLower() != "pending" && eventoShow.Status.ToLower() != "pre_approved")
                  return BadRequest(new { success = false, message = "Esta proposta já foi processada." });

              var musico = await _context.Users.FirstOrDefaultAsync(u => u.Id == eventoShow.UserId);
              if (musico == null || string.IsNullOrWhiteSpace(musico.AsaasWalletId))
                  return BadRequest(new { success = false, message = "Artista sem subconta ativa no Asaas." });
                          var enderecoContratante = await _context.ArtistAddresses.FirstOrDefaultAsync(a => a.UserId == contratanteGuid);
              string cepSeguro = enderecoContratante?.ZipCode ?? "86430000";
              string numeroSeguro = enderecoContratante?.Number ?? "1";

              string asaasCustomerId = await _asaasService.CriarClienteAsync(
                  contratante.NomeCompleto, contratante.Email, contratante.CPF, cepSeguro, numeroSeguro
              );

              var environment = _configuration["AsaasSettings:Environment"] ?? "Sandbox";
              var asaasUrl = _configuration[$"AsaasSettings:{environment}:BaseUrl"] ?? "https://asaas.com";
              var asaasToken = _configuration[$"AsaasSettings:{environment}:ApiKey"];

              decimal valorBrutoContratacao = Math.Round(eventoShow.TotalProposedPrice, 2);

              // 🔒 MODELO REGULAMENTAR ESCROW: O Asaas exige percentualValue (95%) calculado sobre o netValue
              var asaasPayload = new Dictionary<string, object>
              {
                  { "customer", asaasCustomerId },
                  { "billingType", "PIX" },
                  { "value", valorBrutoContratacao },
                  { "dueDate", DateTime.UtcNow.ToString("yyyy-MM-dd") },
                  { "description", $"Cachê em Custódia - Pix Proposta {eventoShow.Id}" },
                  { "split", new[] { new { walletId = musico.AsaasWalletId, percentualValue = 95.00 } } }
              };
              _httpClient.DefaultRequestHeaders.Clear();
              _httpClient.DefaultRequestHeaders.Add("access_token", asaasToken);
              _httpClient.DefaultRequestHeaders.Add("User-Agent", "SevenShowsAPI");

              var jsonOpcoes = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
              var jsonContent = new StringContent(JsonSerializer.Serialize(asaasPayload, jsonOpcoes), System.Text.Encoding.UTF8, "application/json");

              var response = await _httpClient.PostAsync($"{asaasUrl}/payments", jsonContent);
              var responseString = await response.Content.ReadAsStringAsync();

              if (!response.IsSuccessStatusCode) 
                  return BadRequest(new { success = false, message = "Falha Asaas.", erroDetalhado = responseString });

              using var jsonDoc = JsonDocument.Parse(responseString);
              var paymentId = jsonDoc.RootElement.GetProperty("id").GetString();

              eventoShow.AsaasPaymentId = paymentId;
              eventoShow.Status = "Pending";
              await _context.SaveChangesAsync();

              var qrCodeResponse = await _httpClient.GetAsync($"{asaasUrl}/payments/{paymentId}/pixQrCode");
              var qrCodeString = await qrCodeResponse.Content.ReadAsStringAsync();

              return Ok(new { success = true, paymentId, pixDetails = qrCodeString });
          }
          catch (Exception ex) 
          { 
              return StatusCode(500, new { success = false, error = ex.Message }); 
          }
      }


        // ====================================================================
        // 🔍 CONSULTA REATIVA: Verifica se o show foi liquidado pelo Webhook
        // ====================================================================
        [HttpGet("status-pagamento/{id}")]
        public async Task<IActionResult> VerificarStatus(Guid id)
        {
            try
            {
                var statusShow = await _context.ArtistEvents
                    .Where(e => e.Id == id)
                    .Select(e => e.Status)
                    .FirstOrDefaultAsync();

                if (statusShow == null)
                    return NotFound(new { success = false, message = "Show não localizado." });

                bool pago = statusShow.ToLower() == "confirmed" || statusShow.ToLower() == "paid";

                return Ok(new { success = true, isPaid = pago });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}