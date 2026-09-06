using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SevenShows.Api.Modules.Tenants.Services;

public class AsaasService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AsaasService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;

        var env = _configuration["AsaasSettings:Environment"] ?? "Sandbox";
        var baseUrl = _configuration[$"AsaasSettings:{env}:BaseUrl"];
        var apiKey = _configuration[$"AsaasSettings:{env}:ApiKey"];

        if (baseUrl != null && !baseUrl.EndsWith("/"))
        {
            baseUrl += "/";
        }

        _httpClient.BaseAddress = new Uri(baseUrl!);
        _httpClient.DefaultRequestHeaders.Add("access_token", apiKey);
        
        // NOVO: Adiciona o cabeçalho obrigatório exigido pelo gateway Asaas
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SevenShows-API/1.0");
    }

    // 1. Cria o Cliente (Músico) no painel do Asaas
    public async Task<string> CriarClienteAsync(string name, string email, string cpfCnpj, string postalCode, string addressNumber)
    {
        var payload = new
        {
            name,
            email,
            cpfCnpj,
            postalCode,
            addressNumber,
            notificationDisabled = true
        };

        var response = await _httpClient.PostAsJsonAsync("customers", payload);
        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync();
            
            try
            {
                using var doc = JsonDocument.Parse(errorJson);
                var errorsArray = doc.RootElement.GetProperty("errors");
                
                // Acessa o primeiro item da lista de erros [0] de forma correta
                var firstError = errorsArray[0]; 
                var description = firstError.GetProperty("description").GetString();
                
                throw new Exception($"Asaas Recusou: {description}");
            }
            catch (Exception ex) when (!ex.Message.StartsWith("Asaas Recusou:"))
            {
                throw new Exception($"Erro de validação no Asaas: {errorJson}");
            }
        }

        var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return jsonDoc!.RootElement.GetProperty("id").GetString()!;
    }

    // 2. Dispara a assinatura recorrente real para o painel do Asaas
    public async Task<JsonElement> CriarAssinaturaAsync(string customerId, string billingType, decimal value, string description)
    {
        var payload = new
        {
            customer = customerId,
            billingType = billingType == "PIX" ? "PIX" : "CREDIT_CARD",
            nextDueDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"), // Primeiro vencimento amanhã
            value,
            cycle = "MONTHLY",
            description
        };

        var response = await _httpClient.PostAsJsonAsync("subscriptions", payload);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erro ao criar assinatura no Asaas: {error}");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // 1. Busca a lista de faturas geradas por uma assinatura para achar a primeira pendente
    public async Task<string> ObterFaturaAtualDaAssinaturaAsync(string subscriptionId)
    {
        // Faz um GET filtrando as cobranças pertencentes àquela assinatura específica
        var response = await _httpClient.GetAsync($"payments?subscription={subscriptionId}");
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erro ao buscar faturas da assinatura no Asaas: {error}");
        }

        var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var dataArray = jsonDoc!.RootElement.GetProperty("data");
        
        // Pega o ID da primeira fatura gerada na lista
        if (dataArray.GetArrayLength() == 0)
            throw new Exception("Nenhuma fatura foi gerada para esta assinatura ainda.");

        return dataArray[0].GetProperty("id").GetString()!;
    }

    // 2. Busca o QR Code e a chave Copia e Cola em tempo real usando o ID da fatura acima
    public async Task<JsonElement> ObterQrCodePixDaFaturaAsync(string paymentId)
    {
        var response = await _httpClient.GetAsync($"payments/{paymentId}/pixQrCode");
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erro ao gerar QR Code do PIX no Asaas: {error}");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

}
