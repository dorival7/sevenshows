using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SevenShows.Api.Modules.Tenants.Services;

public class AsaasWalletService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AsaasWalletService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;

        var env = _configuration["AsaasShowsSettings:Environment"] ?? "Sandbox";
        var baseUrl = _configuration[$"AsaasShowsSettings:{env}:BaseUrl"];
        var apiKey = _configuration[$"AsaasShowsSettings:{env}:ApiKey"];

        if (baseUrl != null && !baseUrl.EndsWith("/")) baseUrl += "/";

        _httpClient.BaseAddress = new Uri(baseUrl!);
        _httpClient.DefaultRequestHeaders.Add("access_token", apiKey);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SevenShows-API/1.0");
    }

    public async Task<JsonElement> CriarSubcontaParceiroAsync(
        string name, 
        string email, 
        string cpfCnpj, 
        string personType, 
        string postalCode, 
        string addressNumber,
        DateTime birthDate,
        string userCpf,
        string mobilePhone,
        decimal incomeValue,
        string? companyType)
    {
        bool ehPessoaJuridica = personType == "Legal";
        string dataFormatada = birthDate.ToString("yyyy-MM-dd");

        var payload = new Dictionary<string, object>
        {
            { "name", name },
            { "email", email },
            { "cpfCnpj", cpfCnpj },
            { "personType", ehPessoaJuridica ? "JURIDICA" : "FISICA" },
            { "postalCode", postalCode },
            { "addressNumber", addressNumber },
            { "mobilePhone", mobilePhone },
            { "incomeValue", incomeValue },
            { "webhooks", new[] { new { url = "https://sua-api-producao.com", email = email, enabled = true, interrupted = false } } }
        };

        if (ehPessoaJuridica)
        {
            payload.Add("companyType", companyType ?? "MEI");
            payload.Add("companyResponsible", new
            {
                name = name, 
                cpf = userCpf, 
                birthDate = dataFormatada 
            });
        }
        else
        {
            payload.Add("birthDate", dataFormatada);
        }

        var response = await _httpClient.PostAsJsonAsync("accounts", payload);
        
        // CORREÇÃO: Trata a falha antes de ler o JsonElement para evitar o erro de dicionário
        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync();
            throw new Exception($"O Asaas Sandbox recusou a criação da subconta. Resposta do Gateway: {errorJson}");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
