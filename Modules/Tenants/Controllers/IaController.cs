using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Globalization;

namespace SevenShows.Api.Modules.Tenants.Controllers;

public class TransposeCifraRequest
{
  [JsonPropertyName("htmlEstruturado")] public string HtmlEstruturado { get; set; } = string.Empty;
  [JsonPropertyName("tomOriginal")] public string TomOriginal { get; set; } = string.Empty;
  [JsonPropertyName("tomDesejado")] public string TomDesejado { get; set; } = string.Empty;
}

public class MusicaRelacionadaDto
{
  public string Titulo { get; set; } = string.Empty;
  public string Artista { get; set; } = string.Empty;
  public string TermoBusca { get; set; } = string.Empty;
  public string Url { get; set; } = string.Empty;
}

public class GenerateCifraRelacionadaRequest
{
  [JsonPropertyName("url")]
  public string Url { get; set; } = string.Empty;
}

public class CifraClubCandidate
{
  public string Url { get; set; } = string.Empty;
  public string Titulo { get; set; } = string.Empty;
  public string Artista { get; set; } = string.Empty;
  public string TomOriginal { get; set; } = string.Empty;
  public List<string> Acordes { get; set; } = new();
  public List<string> LinhasMusicais { get; set; } = new();
  public int Score { get; set; }
}

public class OptimizeSetlistRequest
{
  public List<string> Estilos { get; set; } = new();
  public string PerfilPublico { get; set; } = string.Empty;
  public int QtdMusicas { get; set; }

  // Brasileiro | Internacional | Misto
  public string TipoRepertorio { get; set; } = "Misto";

  // Livre | Atual | Anos 2000 | Anos 80/90 | Clássicos
  public string Epoca { get; set; } = "Livre";
}

public class GenerateCifraRequest
{
  public string NomeMusica { get; set; } = string.Empty;
  public string NomeArtista { get; set; } = string.Empty; // ADICIONADO: Propriedade que resolve o erro CS1061
  public string TomDesejado { get; set; } = string.Empty;
}

public record GroqTool(
    [property: JsonPropertyName("type")] string Type);

public record GroqCompoundTools(
    [property: JsonPropertyName("enabled_tools")]
    string[] EnabledTools);

public record GroqCompoundCustom(
    [property: JsonPropertyName("tools")]
    GroqCompoundTools Tools);

public record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public record GroqResponseFormat(
    [property: JsonPropertyName("type")] string Type);

public class GroqRequest
{
  [JsonPropertyName("model")] public string Model { get; init; }
  [JsonPropertyName("messages")] public ChatMessage[] Messages { get; init; }
  [JsonPropertyName("response_format")] public GroqResponseFormat ResponseFormat { get; init; }
  [JsonPropertyName("max_tokens")] public int MaxTokens { get; init; } = 1200;
  [JsonPropertyName("temperature")] public double Temperature { get; init; } = 0.7;
  [JsonPropertyName("top_p")] public double TopP { get; init; } = 0.9;
  [JsonPropertyName("stream")] public bool Stream { get; init; } = false;

  public GroqRequest(string model, ChatMessage[] messages, GroqResponseFormat responseFormat,
      int maxTokens = 1200, double temperature = 0.7, double topP = 0.9, bool stream = false)
  {
    Model = model;
    Messages = messages;
    ResponseFormat = responseFormat;
    MaxTokens = maxTokens;
    Temperature = temperature;
    TopP = topP;
    Stream = stream;
  }
}

public class GroqChoice
{
  [JsonPropertyName("message")] public JsonElement Message { get; set; }
  [JsonPropertyName("finish_reason")] public string FinishReason { get; set; } = string.Empty;
  [JsonPropertyName("index")] public int Index { get; set; }
}

public class GroqResponse
{
  [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
  [JsonPropertyName("object")] public string Object { get; set; } = string.Empty;
  [JsonPropertyName("created")] public long Created { get; set; }
  [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
  [JsonPropertyName("choices")] public GroqChoice[] Choices { get; set; } = Array.Empty<GroqChoice>();
}

[ApiController]
[Route("api/artists/ia")]
[Authorize]
public class IaController : ControllerBase
{
  private readonly IConfiguration _configuration;
  private static readonly HttpClient _http = new HttpClient();

  public IaController(IConfiguration configuration)
  {
    _configuration = configuration;

    if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
    {
      _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

  }

  [HttpPost("optimize-setlist")]
  public async Task<ActionResult> OptimizeSetlistReal(
    [FromBody] OptimizeSetlistRequest request)
  {
    // ================================================================
    // 1. VALIDAÇÕES
    // ================================================================
    if (request == null)
    {
      return BadRequest(new
      {
        mensagem = "Requisição inválida."
      });
    }

    if (request.Estilos == null || request.Estilos.Count == 0)
    {
      return BadRequest(new
      {
        mensagem = "Selecione pelo menos um estilo musical."
      });
    }

    if (request.Estilos.Count > 3)
    {
      return BadRequest(new
      {
        mensagem = "Selecione no máximo 3 estilos musicais."
      });
    }

    if (string.IsNullOrWhiteSpace(request.PerfilPublico))
    {
      return BadRequest(new
      {
        mensagem =
              "Informe o perfil do público ou conceito do evento."
      });
    }

    if (request.QtdMusicas < 5 || request.QtdMusicas > 30)
    {
      return BadRequest(new
      {
        mensagem =
              "A quantidade de músicas deve estar entre 5 e 30."
      });
    }

    string tipoRepertorio =
        string.IsNullOrWhiteSpace(request.TipoRepertorio)
            ? "Misto"
            : request.TipoRepertorio.Trim();

    string epoca =
        string.IsNullOrWhiteSpace(request.Epoca)
            ? "Livre"
            : request.Epoca.Trim();

    string estilosTexto =
        string.Join(", ", request.Estilos);

    string perfilPublico =
        request.PerfilPublico.Trim();

    // ================================================================
    // 2. CONFIGURAÇÃO GROQ
    // ================================================================
    string? groqBaseUrl =
        _configuration["Groq:BaseUrl"];

    string? groqApiKey =
        _configuration["Groq:ApiKey"];

    if (string.IsNullOrWhiteSpace(groqBaseUrl) ||
        string.IsNullOrWhiteSpace(groqApiKey))
    {
      return StatusCode(500, new
      {
        mensagem =
              "Configuração Groq ausente no appsettings.json."
      });
    }

    string urlFinal =
        groqBaseUrl.EndsWith("/")
            ? $"{groqBaseUrl}chat/completions"
            : $"{groqBaseUrl}/chat/completions";

    // ================================================================
    // 3. ESTRUTURAS INTERNAS
    // ================================================================
    var musicasFinais =
        new List<(
            string Titulo,
            string Artista,
            string Justificativa
        )>();

    var chavesMusicas =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

    // ================================================================
    // 4. NORMALIZAÇÃO PARA DUPLICIDADES
    // ================================================================
    string NormalizarTexto(string texto)
    {
      if (string.IsNullOrWhiteSpace(texto))
        return string.Empty;

      string valor =
          texto
              .Trim()
              .ToUpperInvariant()
              .Normalize(
                  NormalizationForm.FormD
              );

      var sb = new StringBuilder();

      foreach (char caractere in valor)
      {
        UnicodeCategory categoria =
            CharUnicodeInfo.GetUnicodeCategory(
                caractere
            );

        if (categoria !=
            UnicodeCategory.NonSpacingMark)
        {
          sb.Append(caractere);
        }
      }

      valor =
          sb
              .ToString()
              .Normalize(
                  NormalizationForm.FormC
              );

      valor = Regex.Replace(
          valor,
          @"[^\p{L}\p{N}\s]",
          " "
      );

      valor = Regex.Replace(
          valor,
          @"\s+",
          " "
      );

      return valor.Trim();
    }

    string CriarChaveMusica(
        string titulo,
        string artista)
    {
      return
          $"{NormalizarTexto(titulo)}|" +
          $"{NormalizarTexto(artista)}";
    }

    // ================================================================
    // 5. CURVA DO SHOW
    // ================================================================
    string ObterCurvaPorPosicao(
        int posicao,
        int total)
    {
      if (total <= 0)
        return "Abertura";

      double percentual =
          (double)posicao / total;

      if (percentual <= 0.20)
        return "Abertura";

      if (percentual <= 0.45)
        return "Crescimento";

      if (percentual <= 0.70)
        return "Respiro";

      if (percentual <= 0.90)
        return "Pico";

      return "Final";
    }

    string ObterOrientacaoCurva(
        int posicaoInicial,
        int quantidade,
        int total)
    {
      int posicaoFinal =
          Math.Min(
              posicaoInicial +
              quantidade -
              1,
              total
          );

      string inicio =
          ObterCurvaPorPosicao(
              posicaoInicial,
              total
          );

      string fim =
          ObterCurvaPorPosicao(
              posicaoFinal,
              total
          );

      if (inicio == fim)
        return inicio;

      return
          $"{inicio} para {fim}";
    }

    // ================================================================
    // 6. TEMPO DE RETRY DO 429
    // ================================================================
    double ExtrairSegundosRetry(
        string corpo)
    {
      if (string.IsNullOrWhiteSpace(corpo))
        return 12;

      Match match = Regex.Match(
          corpo,
          @"try again in\s+([0-9]+(?:\.[0-9]+)?)s",
          RegexOptions.IgnoreCase
      );

      if (
          match.Success &&
          double.TryParse(
              match.Groups[1].Value,
              NumberStyles.Any,
              CultureInfo.InvariantCulture,
              out double segundos
          )
      )
      {
        return segundos;
      }

      return 12;
    }

    // ================================================================
    // 7. LEITURA SEGURA DO CONTENT
    //
    // Não assumimos mais que:
    // choices[0].message.content
    // sempre existe ou sempre é string.
    // ================================================================
    string ExtrairContentGroq(
        string corpo)
    {
      if (string.IsNullOrWhiteSpace(corpo))
        return string.Empty;

      try
      {
        using JsonDocument documento =
            JsonDocument.Parse(corpo);

        JsonElement root =
            documento.RootElement;

        if (!root.TryGetProperty(
                "choices",
                out JsonElement choices))
        {
          return string.Empty;
        }

        if (choices.ValueKind !=
            JsonValueKind.Array)
        {
          return string.Empty;
        }

        if (choices.GetArrayLength() == 0)
        {
          return string.Empty;
        }

        JsonElement choice =
            choices[0];

        if (!choice.TryGetProperty(
                "message",
                out JsonElement message))
        {
          return string.Empty;
        }

        if (message.ValueKind !=
            JsonValueKind.Object)
        {
          return string.Empty;
        }

        if (!message.TryGetProperty(
                "content",
                out JsonElement content))
        {
          return string.Empty;
        }

        if (content.ValueKind ==
            JsonValueKind.String)
        {
          return
              content.GetString()?.Trim()
              ?? string.Empty;
        }

        return string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    // ================================================================
    // 8. CHAMADA GROQ ROBUSTA
    //
    // Faz retry para:
    //
    // - HTTP 429
    // - HTTP 200 com content vazio
    //
    // O retry acontece DENTRO do mesmo lote.
    // ================================================================
    async Task<string> ChamarGroqTexto(
        string promptSistema,
        string promptUsuario)
    {
      const int maxRetries429 = 5;
      const int maxRetriesVazio = 2;

      int retries429 = 0;
      int retriesVazio = 0;

      while (true)
      {
        var payload = new
        {
            model = "openai/gpt-oss-20b",

            messages = new object[]
            {
                new
                {
                    role = "user",
                    content =
                        promptSistema +
                        "\n\n" +
                        promptUsuario
                }
            },

            reasoning_effort = "low",

            include_reasoning = false,

            temperature = 0.5,

            max_completion_tokens = 1200
        };

        string jsonRequest =
            JsonSerializer.Serialize(
                payload
            );

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                urlFinal
            );

        httpRequest.Headers
            .TryAddWithoutValidation(
                "Authorization",
                $"Bearer {groqApiKey.Trim()}"
            );

        httpRequest.Content =
            new StringContent(
                jsonRequest,
                Encoding.UTF8,
                "application/json"
            );

        using var cts =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(90)
            );

        HttpResponseMessage resposta;

        try
        {
          resposta =
              await _http.SendAsync(
                  httpRequest,
                  cts.Token
              );
        }
        catch (TaskCanceledException)
        {
          throw new TimeoutException(
              "Timeout ao aguardar resposta da Groq."
          );
        }

        using (resposta)
        {
          string corpo =
              await resposta.Content
                  .ReadAsStringAsync();

          // ====================================================
          // HTTP 429
          // ====================================================
          if ((int)resposta.StatusCode == 429)
          {
            retries429++;

            if (retries429 > maxRetries429)
            {
              throw new Exception(
                  "A Groq permaneceu em rate limit " +
                  "mesmo após as esperas automáticas."
              );
            }

            double segundos =
                ExtrairSegundosRetry(
                    corpo
                );

            // margem adicional
            segundos += 3;

            segundos =
                Math.Max(
                    5,
                    Math.Min(
                        segundos,
                        70
                    )
                );

            Console.WriteLine(
                $"[SETLIST] Rate limit. " +
                $"Aguardando {segundos:F1}s. " +
                $"Retry {retries429}/" +
                $"{maxRetries429}."
            );

            await Task.Delay(
                TimeSpan.FromSeconds(
                    segundos
                )
            );

            continue;
          }

          // ====================================================
          // OUTRO ERRO HTTP
          // ====================================================
          if (!resposta.IsSuccessStatusCode)
          {
            throw new Exception(
                $"Groq retornou HTTP " +
                $"{(int)resposta.StatusCode}: " +
                $"{corpo}"
            );
          }

          // ====================================================
          // HTTP 200
          // ====================================================
          string content =
              ExtrairContentGroq(
                  corpo
              );

          if (!string.IsNullOrWhiteSpace(
                  content
              ))
          {
            return content;
          }

          // ====================================================
          // CONTENT VAZIO
          // ====================================================
          retriesVazio++;

          if (retriesVazio >
              maxRetriesVazio)
          {
            throw new Exception(
                "Groq retornou conteúdo vazio " +
                "após as tentativas automáticas."
            );
          }

          Console.WriteLine(
              $"[SETLIST] Groq retornou content vazio. " +
              $"Aguardando 2s e repetindo o mesmo lote. " +
              $"Retry {retriesVazio}/" +
              $"{maxRetriesVazio}."
          );

          await Task.Delay(
              TimeSpan.FromSeconds(2)
          );
        }
      }
    }

    // ================================================================
    // 9. PARSER
    //
    // Formato esperado:
    //
    // Título | Artista | Justificativa
    // ================================================================
    List<(
        string Titulo,
        string Artista,
        string Justificativa
    )> InterpretarResposta(
        string resposta)
    {
      var resultado =
          new List<(
              string Titulo,
              string Artista,
              string Justificativa
          )>();

      if (string.IsNullOrWhiteSpace(resposta))
        return resultado;

      string texto =
          resposta
              .Replace(
                  "```text",
                  "",
                  StringComparison.OrdinalIgnoreCase
              )
              .Replace(
                  "```txt",
                  "",
                  StringComparison.OrdinalIgnoreCase
              )
              .Replace(
                  "```",
                  ""
              )
              .Trim();

      string[] linhas =
          texto.Split(
              new[]
              {
                    "\r\n",
                    "\n",
                    "\r"
              },
              StringSplitOptions
                  .RemoveEmptyEntries
          );

      foreach (
          string linhaOriginal
          in linhas
      )
      {
        string linha =
            linhaOriginal.Trim();

        if (string.IsNullOrWhiteSpace(
            linha
        ))
        {
          continue;
        }

        // Remove numeração eventual:
        // 1.
        // 1)
        // 01 -
        linha = Regex.Replace(
            linha,
            @"^\s*\d+\s*[\.\)\-\:]\s*",
            ""
        );

        if (!linha.Contains("|"))
          continue;

        string[] partes =
            linha.Split('|');

        if (partes.Length < 2)
          continue;

        string titulo =
            partes[0].Trim();

        string artista =
            partes[1].Trim();

        string justificativa =
            partes.Length >= 3
                ? string.Join(
                    " | ",
                    partes.Skip(2)
                ).Trim()
                : "Boa escolha para o perfil do público.";

        titulo = Regex.Replace(
            titulo,
            @"^(t[ií]tulo|m[uú]sica)\s*:\s*",
            "",
            RegexOptions.IgnoreCase
        ).Trim();

        artista = Regex.Replace(
            artista,
            @"^artista\s*:\s*",
            "",
            RegexOptions.IgnoreCase
        ).Trim();

        justificativa =
            Regex.Replace(
                justificativa,
                @"^(justificativa|estrat[eé]gia)\s*:\s*",
                "",
                RegexOptions.IgnoreCase
            ).Trim();

        if (string.IsNullOrWhiteSpace(
                titulo) ||
            string.IsNullOrWhiteSpace(
                artista))
        {
          continue;
        }

        if (titulo.Length < 2 ||
            artista.Length < 2)
        {
          continue;
        }

        resultado.Add(
            (
                titulo,
                artista,
                justificativa
            )
        );
      }

      return resultado;
    }

    // ================================================================
    // 10. PROMPT DE SISTEMA
    // ================================================================
    string promptSistema = """
Você é diretor musical profissional de shows ao vivo.

Selecione somente músicas reais e artistas reais.
Respeite estilo, público, origem, época e fase do show.
Não repita músicas da lista de exclusão.
Evite excesso do mesmo artista.
Priorize músicas reconhecíveis pelo público.

Cada resposta deve conter somente linhas no formato:

Título | Artista | Justificativa curta

Não use JSON.
Não use Markdown.
Não numere.
Não escreva cabeçalho.
Não escreva introdução ou conclusão.
""";

    try
    {
      // ============================================================
      // 11. MOTOR DE GERAÇÃO
      //
      // Lote máximo de 10.
      //
      // 10 → 10
      // 15 → 10 + 5
      // 20 → 10 + 10
      // 30 → 10 + 10 + 10
      //
      // Se um lote vier parcial:
      // calcula automaticamente o que ainda falta.
      // ============================================================
      const int tamanhoMaximoLote = 10;

      // margem para complementações
      const int maximoLotes = 8;

      int numeroLote = 0;

      while (
          musicasFinais.Count <
              request.QtdMusicas &&
          numeroLote <
              maximoLotes
      )
      {
        numeroLote++;

        int faltam =
            request.QtdMusicas -
            musicasFinais.Count;

        int quantidadeLote =
            Math.Min(
                tamanhoMaximoLote,
                faltam
            );

        int posicaoInicial =
            musicasFinais.Count + 1;

        string curva =
            ObterOrientacaoCurva(
                posicaoInicial,
                quantidadeLote,
                request.QtdMusicas
            );

        // ========================================================
        // EXCLUSÃO
        // ========================================================
        string listaExclusao;

        if (musicasFinais.Count == 0)
        {
          listaExclusao =
              "Nenhuma.";
        }
        else
        {
          listaExclusao =
              string.Join(
                  "; ",
                  musicasFinais.Select(
                      m =>
                          $"{m.Titulo} - {m.Artista}"
                  )
              );
        }

        // ========================================================
        // PROMPT DO LOTE
        // ========================================================
        string promptLote = $"""
Gere exatamente {quantidadeLote} músicas novas.

Estilos: {estilosTexto}
Público: {perfilPublico}
Origem: {tipoRepertorio}
Época: {epoca}
Fase: {curva}

Não repetir:
{listaExclusao}

Uma música por linha:

Título | Artista | Justificativa curta

Retorne somente as {quantidadeLote} linhas.
""";

        Console.WriteLine(
            $"[SETLIST] Iniciando lote " +
            $"{numeroLote}. " +
            $"Pedido: {quantidadeLote}. " +
            $"Atual: {musicasFinais.Count}/" +
            $"{request.QtdMusicas}."
        );

        try
        {
          string respostaTexto =
              await ChamarGroqTexto(
                  promptSistema,
                  promptLote
              );

          var musicasRecebidas =
              InterpretarResposta(
                  respostaTexto
              );

          int quantidadeAntes =
              musicasFinais.Count;

          foreach (
              var musica
              in musicasRecebidas
          )
          {
            if (
                musicasFinais.Count >=
                request.QtdMusicas
            )
            {
              break;
            }

            string chave =
                CriarChaveMusica(
                    musica.Titulo,
                    musica.Artista
                );

            if (!chavesMusicas.Add(
                    chave))
            {
              Console.WriteLine(
                  "[SETLIST] Duplicada ignorada: " +
                  $"{musica.Titulo} - " +
                  $"{musica.Artista}"
              );

              continue;
            }

            musicasFinais.Add(
                (
                    musica.Titulo,
                    musica.Artista,
                    musica.Justificativa
                )
            );
          }

          int adicionadas =
              musicasFinais.Count -
              quantidadeAntes;

          Console.WriteLine(
              $"[SETLIST] Lote {numeroLote}: " +
              $"pedido {quantidadeLote}, " +
              $"parser {musicasRecebidas.Count}, " +
              $"novas {adicionadas}, " +
              $"total {musicasFinais.Count}/" +
              $"{request.QtdMusicas}."
          );
        }
        catch (Exception ex)
        {
          // Uma falha de lote não derruba imediatamente
          // todo o repertório.
          Console.WriteLine(
              $"[SETLIST] Falha no lote " +
              $"{numeroLote}/{maximoLotes}:"
          );
        }
      }

      // ============================================================
      // 12. GARANTIA FINAL
      // ================================================================
      if (musicasFinais.Count <
          request.QtdMusicas)
      {
        return StatusCode(502, new
        {
          mensagem =
                "Não foi possível completar o repertório " +
                "com músicas únicas.",

          quantidadeSolicitada =
                request.QtdMusicas,

          quantidadeObtida =
                musicasFinais.Count,

          lotesIA =
                numeroLote
        });
      }

      // Segurança:
      // jamais retornar mais que solicitado.
      musicasFinais =
          musicasFinais
              .Take(
                  request.QtdMusicas
              )
              .ToList();

      // ============================================================
      // 13. RESULTADO FINAL
      // ================================================================
      var setlistFinal =
          musicasFinais
              .Select(
                  (musica, index) =>
                  {
                    int ordem =
                          index + 1;

                    string curvaEnergia =
                          ObterCurvaPorPosicao(
                              ordem,
                              request.QtdMusicas
                          );

                    string justificativa =
                          string.IsNullOrWhiteSpace(
                              musica.Justificativa
                          )
                              ? "Boa escolha para o perfil do público."
                              : musica.Justificativa.Trim();

                    return new
                    {
                      ordem,

                      titulo =
                              musica.Titulo,

                      artistaOriginal =
                              musica.Artista,

                      curvaEnergia,

                      justificativaIA =
                              justificativa
                    };
                  }
              )
              .ToList();

      // ============================================================
      // 14. RESUMO
      // ================================================================
      string resumoEstrategico =
          $"Repertório de {request.QtdMusicas} músicas " +
          $"em {estilosTexto}, direcionado ao perfil informado. " +
          $"Seleção {tipoRepertorio.ToLowerInvariant()}, " +
          $"com preferência {epoca.ToLowerInvariant()}, " +
          $"organizada entre abertura, crescimento, " +
          $"respiro, pico e final.";

      // ============================================================
      // 15. RETORNO
      // ================================================================
      return Ok(new
      {
        resumoEstrategico,

        sugestaoSetlist =
              setlistFinal,

        quantidadeSolicitada =
              request.QtdMusicas,

        quantidadeGerada =
              setlistFinal.Count,

        lotesIA =
              numeroLote,

        filtros = new
        {
          estilos =
                  request.Estilos,

          tipoRepertorio,

          epoca
        }
      });
    }
    catch (OperationCanceledException)
    {
      return StatusCode(504, new
      {
        mensagem =
              "A IA demorou mais que o esperado " +
              "para montar o repertório."
      });
    }
    catch (Exception ex)
    {
      return StatusCode(500, new
      {
        mensagem =
              "Erro interno ao otimizar o repertório.",

        erro =
              ex.Message
      });
    }
  }

  [HttpPost("transpose-cifra")]
  [RequestSizeLimit(52428800)]
  public IActionResult TransposeCifraReal(
    [FromBody] TransposeCifraRequest request)
  {
    try
    {
      // ============================================================
      // 1. VALIDAÇÃO
      // ============================================================

      if (request == null)
      {
        return BadRequest(new
        {
          cifraTransposta = "",
          mensagem = "Payload recebido veio nulo."
        });
      }

      if (string.IsNullOrWhiteSpace(request.HtmlEstruturado))
      {
        return BadRequest(new
        {
          cifraTransposta = "",
          mensagem = "HtmlEstruturado não foi informado."
        });
      }

      if (string.IsNullOrWhiteSpace(request.TomOriginal))
      {
        return BadRequest(new
        {
          cifraTransposta = "",
          mensagem = "TomOriginal não foi informado."
        });
      }

      if (string.IsNullOrWhiteSpace(request.TomDesejado))
      {
        return BadRequest(new
        {
          cifraTransposta = "",
          mensagem = "TomDesejado não foi informado."
        });
      }

      // ============================================================
      // 2. NORMALIZA NOTA
      // ============================================================

      static string NormalizarNota(string nota)
      {
        if (string.IsNullOrWhiteSpace(nota))
          return string.Empty;

        nota = nota
            .Trim()
            .Replace("♯", "#")
            .Replace("♭", "b");

        var match = Regex.Match(
            nota,
            @"^(?<nota>[A-Ga-g])(?<acidente>#|b)?"
        );

        if (!match.Success)
          return nota;

        return
            match.Groups["nota"].Value.ToUpperInvariant() +
            match.Groups["acidente"].Value;
      }

      // ============================================================
      // 3. EXTRAI A RAIZ DO TOM
      // ============================================================

      static string ExtrairRaizTom(string tom)
      {
        if (string.IsNullOrWhiteSpace(tom))
          return string.Empty;

        var match = Regex.Match(
            tom.Trim(),
            @"^(?<nota>[A-Ga-g](?:#|b)?)"
        );

        if (!match.Success)
          return string.Empty;

        return NormalizarNota(
            match.Groups["nota"].Value
        );
      }

      // ============================================================
      // 4. MAPA CROMÁTICO
      // ============================================================

      var mapaNotas =
          new Dictionary<string, int>(
              StringComparer.OrdinalIgnoreCase)
          {
            ["C"] = 0,
            ["B#"] = 0,

            ["C#"] = 1,
            ["Db"] = 1,

            ["D"] = 2,

            ["D#"] = 3,
            ["Eb"] = 3,

            ["E"] = 4,
            ["Fb"] = 4,

            ["F"] = 5,
            ["E#"] = 5,

            ["F#"] = 6,
            ["Gb"] = 6,

            ["G"] = 7,

            ["G#"] = 8,
            ["Ab"] = 8,

            ["A"] = 9,

            ["A#"] = 10,
            ["Bb"] = 10,

            ["B"] = 11,
            ["Cb"] = 11
          };

      string[] notasSustenidos =
      {
            "C", "C#", "D", "D#", "E", "F",
            "F#", "G", "G#", "A", "A#", "B"
        };

      string[] notasBemois =
      {
            "C", "Db", "D", "Eb", "E", "F",
            "Gb", "G", "Ab", "A", "Bb", "B"
        };

      // ============================================================
      // 5. TOM ORIGINAL / TOM DESEJADO
      // ============================================================

      string raizOriginal =
          ExtrairRaizTom(request.TomOriginal);

      string raizDesejada =
          ExtrairRaizTom(request.TomDesejado);

      if (!mapaNotas.TryGetValue(
              raizOriginal,
              out int indiceOriginal))
      {
        return BadRequest(new
        {
          cifraTransposta = "",
          mensagem =
                $"Tom original inválido: {request.TomOriginal}"
        });
      }

      if (!mapaNotas.TryGetValue(
              raizDesejada,
              out int indiceDesejado))
      {
        return BadRequest(new
        {
          cifraTransposta = "",
          mensagem =
                $"Tom desejado inválido: {request.TomDesejado}"
        });
      }

      int deslocamento =
          (indiceDesejado - indiceOriginal + 12) % 12;

      // ============================================================
      // 6. ESCOLHE SUSTENIDOS OU BEMÓIS
      // ============================================================

      bool usarBemois =
          raizDesejada.Contains("b");

      var tonsBemois =
          new HashSet<string>(
              StringComparer.OrdinalIgnoreCase)
          {
                "F",
                "Bb",
                "Eb",
                "Ab",
                "Db",
                "Gb"
          };

      if (tonsBemois.Contains(raizDesejada))
      {
        usarBemois = true;
      }

      string ObterNota(int indice)
      {
        indice =
            ((indice % 12) + 12) % 12;

        return usarBemois
            ? notasBemois[indice]
            : notasSustenidos[indice];
      }

      // ============================================================
      // 7. TRANSPÕE NOTA
      // ============================================================

      string TransporNota(string nota)
      {
        string normalizada =
            NormalizarNota(nota);

        if (!mapaNotas.TryGetValue(
                normalizada,
                out int indice))
        {
          return nota;
        }

        return ObterNota(
            indice + deslocamento
        );
      }

      // ============================================================
      // 8. TRANSPÕE ACORDE COMPLETO
      //
      // B    -> A#
      // F#   -> F
      // G#m  -> Gm
      // F#7  -> F7
      // D/F# -> C#/F
      // ============================================================

      string TransporAcorde(string acorde)
      {
        if (string.IsNullOrWhiteSpace(acorde))
          return acorde;

        string valor =
            HtmlEntity
                .DeEntitize(acorde)
                .Trim();

        var match =
            Regex.Match(
                valor,
                @"^(?<raiz>[A-Ga-g](?:#|b)?)" +
                @"(?<qualidade>[^/]*)" +
                @"(?:/(?<baixo>[A-Ga-g](?:#|b)?))?$"
            );

        if (!match.Success)
          return acorde;

        string raiz =
            match.Groups["raiz"].Value;

        string qualidade =
            match.Groups["qualidade"].Value;

        string baixo =
            match.Groups["baixo"].Value;

        var resultado =
            new StringBuilder();

        resultado.Append(
            TransporNota(raiz)
        );

        resultado.Append(
            qualidade
        );

        if (!string.IsNullOrWhiteSpace(baixo))
        {
          resultado.Append('/');

          resultado.Append(
              TransporNota(baixo)
          );
        }

        return resultado.ToString();
      }

      // ============================================================
      // 9. CARREGA HTML ORIGINAL
      // ============================================================

      var docHtml =
          new HtmlDocument
          {
            OptionWriteEmptyNodes = true
          };

      docHtml.LoadHtml(
          request.HtmlEstruturado
      );

      // ============================================================
      // 10. LOCALIZA SOMENTE <b data-chord-name>
      //
      // Assim nenhum texto da letra pode ser confundido
      // com acorde.
      // ============================================================

      var nosAcordes =
          docHtml.DocumentNode.SelectNodes(
              "//b[@data-chord-name]"
          );

      int acordesProcessados = 0;

      var mapaTransposicao =
          new Dictionary<string, string>(
              StringComparer.OrdinalIgnoreCase
          );

      if (nosAcordes != null)
      {
        foreach (var noAcorde in nosAcordes.ToList())
        {
          string acordeOriginal =
              noAcorde.GetAttributeValue(
                  "data-chord-name",
                  string.Empty
              );

          if (string.IsNullOrWhiteSpace(acordeOriginal))
          {
            acordeOriginal =
                HtmlEntity.DeEntitize(
                    noAcorde.InnerText
                );
          }

          acordeOriginal =
              acordeOriginal.Trim();

          if (string.IsNullOrWhiteSpace(acordeOriginal))
            continue;

          string acordeTransposto =
              TransporAcorde(acordeOriginal);

          // ====================================================
          // 10.1 CALCULA DIFERENÇA DE LARGURA
          //
          // B -> A#
          // diferença +1
          //
          // F# -> F
          // diferença -1
          // ====================================================

          int diferenca =
              acordeTransposto.Length -
              acordeOriginal.Length;

          // ====================================================
          // 10.2 ALTERA SOMENTE O ACORDE
          // ====================================================

          noAcorde.SetAttributeValue(
              "data-chord-name",
              acordeTransposto
          );

          noAcorde.InnerHtml =
              HtmlEntity.Entitize(
                  acordeTransposto
              );

          // ====================================================
          // 10.3 COMPENSAÇÃO HORIZONTAL
          //
          // IMPORTANTE:
          //
          // Esta lógica mantém a coluna inicial dos acordes.
          // Não alterar.
          // ====================================================

          HtmlNode? proximo =
              noAcorde.NextSibling;

          if (proximo != null &&
              proximo.NodeType == HtmlNodeType.Text)
          {
            string textoSeguinte =
                ((HtmlTextNode)proximo).Text;

            if (diferenca > 0)
            {
              // --------------------------------------------
              // O novo acorde ficou MAIOR.
              //
              // Exemplo:
              //
              // B -> A#
              //
              // Retira a diferença dos espaços posteriores.
              // --------------------------------------------

              int quantidadeEspacos = 0;

              while (
                  quantidadeEspacos <
                      textoSeguinte.Length &&
                  textoSeguinte[
                      quantidadeEspacos
                  ] == ' ')
              {
                quantidadeEspacos++;
              }

              int realmenteRemover =
                  Math.Min(
                      diferenca,
                      quantidadeEspacos
                  );

              if (realmenteRemover > 0)
              {
                textoSeguinte =
                    textoSeguinte.Substring(
                        realmenteRemover
                    );
              }
            }
            else if (diferenca < 0)
            {
              // --------------------------------------------
              // O novo acorde ficou MENOR.
              //
              // Exemplo:
              //
              // F# -> F
              //
              // Acrescenta a diferença depois do acorde.
              // --------------------------------------------

              int adicionar =
                  Math.Abs(diferenca);

              textoSeguinte =
                  new string(
                      ' ',
                      adicionar
                  ) +
                  textoSeguinte;
            }

            ((HtmlTextNode)proximo).Text =
                textoSeguinte;
          }
          else if (diferenca < 0)
          {
            // =================================================
            // Caso não exista TextNode depois do acorde
            // =================================================

            int adicionar =
                Math.Abs(diferenca);

            var espaco =
                docHtml.CreateTextNode(
                    new string(
                        ' ',
                        adicionar
                    )
                );

            noAcorde
                .ParentNode
                .InsertAfter(
                    espaco,
                    noAcorde
                );
          }

          acordesProcessados++;

          if (!mapaTransposicao.ContainsKey(
                  acordeOriginal))
          {
            mapaTransposicao.Add(
                acordeOriginal,
                acordeTransposto
            );
          }
        }
      }

      // ============================================================
      // 11. LOCALIZA AS LINHAS kvMV
      // ============================================================

      var linhasDom =
          docHtml.DocumentNode.SelectNodes(
              "//div[contains(" +
              "concat(' ', normalize-space(@class), ' '), " +
              "' kvMV '" +
              ")]"
          );

      if (linhasDom == null ||
          linhasDom.Count == 0)
      {
        return BadRequest(new
        {
          cifraTransposta = "",
          mensagem =
                "Nenhuma linha kvMV foi encontrada no HTML."
        });
      }

      // ============================================================
      // 12. EXTRAI UMA LINHA PRESERVANDO ESPAÇOS HORIZONTAIS
      // ============================================================

      static string ExtrairLinha(
          HtmlNode linha)
      {
        var sb =
            new StringBuilder();

        void Percorrer(HtmlNode node)
        {
          foreach (var filho in node.ChildNodes)
          {
            // ================================================
            // TEXT NODE
            // ================================================

            if (filho.NodeType ==
                HtmlNodeType.Text)
            {
              string texto =
                  ((HtmlTextNode)filho).Text;

              texto =
                  HtmlEntity.DeEntitize(
                      texto
                  );

              // --------------------------------------------
              // NÃO FAZER:
              //
              // texto.Trim()
              // texto.TrimStart()
              // Regex.Replace(texto, @"\s+", " ")
              //
              // Esses espaços determinam a posição
              // horizontal dos acordes.
              // --------------------------------------------

              sb.Append(texto);

              continue;
            }

            // ================================================
            // <br>
            // ================================================

            if (filho.Name.Equals(
                    "br",
                    StringComparison.OrdinalIgnoreCase))
            {
              sb.Append('\n');

              continue;
            }

            // ================================================
            // OUTROS NÓS
            // ================================================

            Percorrer(filho);
          }
        }

        Percorrer(linha);

        return sb.ToString();
      }

      // ============================================================
      // 13. EXTRAI TODAS AS LINHAS
      // ============================================================

      var linhasTexto =
          new List<string>();

      foreach (var linhaDom in linhasDom)
      {
        string linha =
            ExtrairLinha(
                linhaDom
            );

        linhasTexto.Add(
            linha
        );
      }

      // ============================================================
      // 14. NOVA REGRA DE ESPAÇAMENTO VERTICAL
      //
      // DIFERENÇA PARA A VERSÃO ANTERIOR:
      //
      // Antes:
      // mantínhamos uma linha vazia.
      //
      // Agora:
      // linhas kvMV completamente vazias NÃO entram
      // no texto final.
      //
      // IMPORTANTE:
      //
      // Isso NÃO modifica os espaços de nenhuma
      // linha que possui conteúdo.
      //
      // Portanto:
      //
      // - posição horizontal continua intacta;
      // - acordes continuam sobre as sílabas;
      // - somente o excesso vertical é removido.
      // ============================================================

      var linhasFinais =
          new List<string>();

      foreach (var linha in linhasTexto)
      {
        // --------------------------------------------------------
        // Se a div kvMV não possui nenhum conteúdo real,
        // não gera uma linha no <pre>.
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(linha))
        {
          continue;
        }

        // --------------------------------------------------------
        // MUITO IMPORTANTE:
        //
        // Adicionamos a linha ORIGINAL.
        //
        // Não usamos Trim().
        //
        // Portanto todos os espaços horizontais continuam
        // exatamente como foram calculados.
        // --------------------------------------------------------

        linhasFinais.Add(
            linha
        );
      }

      // ============================================================
      // 15. MONTA CIFRA FINAL
      //
      // Exatamente UMA quebra entre cada linha útil.
      // ============================================================

      string cifraTransposta =
          string.Join(
              "\n",
              linhasFinais
          );

      // ============================================================
      // 16. HTML TRANPOSTO
      //
      // O Vue recebe este HTML para utilizar como base
      // na próxima transposição.
      // ============================================================

      string htmlTransposto =
          docHtml.DocumentNode.InnerHtml;

      // ============================================================
      // 17. RETORNO
      // ============================================================

      return Ok(new
      {
        cifraTransposta,

        htmlEstruturado =
              htmlTransposto,

        tomOriginal =
              request.TomOriginal,

        tomDesejado =
              request.TomDesejado,

        semitons =
              deslocamento,

        acordesProcessados,

        mapaAcordes =
              mapaTransposicao
      });
    }
    catch (Exception ex)
    {
      return StatusCode(500, new
      {
        cifraTransposta = "",

        mensagem =
              "Erro ao transpor a cifra localmente.",

        erro =
              ex.Message
      });
    }
  }

  [HttpPost("generate-cifra")]
  public async Task<IActionResult> GenerateCifraReal(
      [FromBody] GenerateCifraRequest request)
  {
    if (request == null ||
        string.IsNullOrWhiteSpace(request.NomeMusica))
    {
      return BadRequest(new
      {
        erro = "Informe o nome da música."
      });
    }

    try
    {
      // ============================================================
      // 1. BUSCA NORMAL
      //
      // SOMENTE ESTE ENDPOINT USA TAVILY.
      // ============================================================

      string queryTavily;

      if (!string.IsNullOrWhiteSpace(request.NomeArtista))
      {
        queryTavily =
            $"{request.NomeArtista.Trim()} " +
            $"\"{request.NomeMusica.Trim()}\"";
      }
      else
      {
        queryTavily =
            $"\"{request.NomeMusica.Trim()}\"";
      }

      var tavilyUrlFinal =
          "https://api.tavily.com/search";

      var payloadBusca = new
      {
        query = queryTavily,
        auto_parameters = false,
        topic = "general",
        search_depth = "fast",
        max_results = 5,

        include_domains = new[]
          {
                "cifraclub.com.br"
            },

        exact_match = true,
        include_answer = "basic"
      };

      using var cts =
          new CancellationTokenSource(
              TimeSpan.FromSeconds(25)
          );

      var payloadStr =
          JsonSerializer.Serialize(payloadBusca);

      using var conteudo =
          new StringContent(
              payloadStr,
              Encoding.UTF8,
              "application/json"
          );

      _http.DefaultRequestHeaders.Clear();

      // ============================================================
      // MANTENHA AQUI A MESMA CHAVE TAVILY QUE JÁ USA.
      //
      // Idealmente:
      //
      // var tokenTavily =
      //     _configuration["Tavily:ApiKey"];
      // ============================================================

      var tokenTavily =
          _configuration["Tavily:ApiKey"];

      if (string.IsNullOrWhiteSpace(tokenTavily))
      {
        return StatusCode(500, new
        {
          mensagem =
                "Chave Tavily não configurada."
        });
      }

      _http.DefaultRequestHeaders.Add(
          "Authorization",
          "Bearer " + tokenTavily
      );

      var respostaTavily =
          await _http.PostAsync(
              tavilyUrlFinal,
              conteudo,
              cts.Token
          );

      if (!respostaTavily.IsSuccessStatusCode)
      {
        return Ok(new
        {
          cifraCompleta =
                "Erro na busca do Seven Shows."
        });
      }

      var respostaJsonStr =
          await respostaTavily.Content
              .ReadAsStringAsync();

      using var jsonDoc =
          JsonDocument.Parse(
              respostaJsonStr
          );

      if (!jsonDoc.RootElement.TryGetProperty(
              "results",
              out var resArray) ||
          resArray.GetArrayLength() == 0)
      {
        return Ok(new
        {
          cifraCompleta =
                "Música não localizada no índice do Seven Shows."
        });
      }

      // ============================================================
      // LOCALIZA URL REAL DO CIFRA CLUB
      // ============================================================

      string urlCifraReal =
          string.Empty;

      foreach (
          JsonElement itemResult
          in resArray.EnumerateArray())
      {
        if (!itemResult.TryGetProperty(
                "url",
                out var propriedadeUrl))
        {
          continue;
        }

        string urlCand =
            propriedadeUrl.GetString()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(urlCand))
        {
          continue;
        }

        if (urlCand.Contains(
                "/letra",
                StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        if (!Uri.TryCreate(
                urlCand,
                UriKind.Absolute,
                out var uriValidada))
        {
          continue;
        }

        // Só aceita Cifra Club.
        if (!HostCifraClubValido(
                uriValidada.Host))
        {
          continue;
        }

        var segmentos =
            uriValidada.AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            );

        // Esperado:
        //
        // /artista/musica/
        if (segmentos.Length != 2)
        {
          continue;
        }

        urlCifraReal =
            uriValidada
                .GetLeftPart(UriPartial.Path);

        break;
      }

      if (string.IsNullOrWhiteSpace(
              urlCifraReal))
      {
        return Ok(new
        {
          cifraCompleta =
                "Nenhuma folha de cifra correspondente " +
                "localizada após a filtragem de rotas."
        });
      }

      if (!urlCifraReal.EndsWith("/"))
      {
        urlCifraReal += "/";
      }

      // ============================================================
      // A PARTIR DAQUI NÃO HÁ MAIS TAVILY.
      //
      // Envia a URL encontrada para o mesmo processador utilizado
      // pelas músicas relacionadas.
      // ============================================================

      return await ProcessarCifraClubUrl(
          urlCifraReal,
          request.NomeMusica,
          cts.Token
      );

    }
    catch (OperationCanceledException)
    {
      return StatusCode(408, new
      {
        mensagem =
              "Tempo limite excedido ao buscar a cifra."
      });
    }
    catch (Exception ex)
    {
      return StatusCode(500, new
      {
        mensagem =
              "Erro crítico de barramento.",

        erro =
              ex.Message
      });
    }
  }


  // ====================================================================
  // MÚSICA RELACIONADA
  //
  // NÃO USA TAVILY.
  //
  // Recebe diretamente a URL que já veio da página anterior.
  // ====================================================================

  [HttpPost("generate-cifra-related")]
  public async Task<IActionResult> GenerateCifraRelacionadaReal(
      [FromBody] GenerateCifraRelacionadaRequest request)
  {
    if (request == null ||
        string.IsNullOrWhiteSpace(request.Url))
    {
      return BadRequest(new
      {
        mensagem =
              "A URL da música relacionada não foi informada."
      });
    }

    try
    {
      // ============================================================
      // VALIDAÇÃO DE SEGURANÇA
      //
      // Nunca fazemos GET arbitrário de uma URL recebida do Vue.
      // ============================================================

      if (!Uri.TryCreate(
              request.Url.Trim(),
              UriKind.Absolute,
              out var uriRelacionada))
      {
        return BadRequest(new
        {
          mensagem =
                "URL da música relacionada inválida."
        });
      }

      if (!string.Equals(
              uriRelacionada.Scheme,
              Uri.UriSchemeHttps,
              StringComparison.OrdinalIgnoreCase))
      {
        return BadRequest(new
        {
          mensagem =
                "A URL da música relacionada deve usar HTTPS."
        });
      }

      if (!HostCifraClubValido(
              uriRelacionada.Host))
      {
        return BadRequest(new
        {
          mensagem =
                "A URL informada não pertence ao Cifra Club."
        });
      }

      var segmentos =
          uriRelacionada.AbsolutePath.Split(
              '/',
              StringSplitOptions.RemoveEmptyEntries
          );

      // Aceitamos exclusivamente:
      //
      // /artista/musica/
      if (segmentos.Length != 2)
      {
        return BadRequest(new
        {
          mensagem =
                "A URL informada não corresponde a uma página de cifra."
        });
      }

      if (segmentos.Any(
              x =>
                  x.Equals(
                      "search",
                      StringComparison.OrdinalIgnoreCase) ||
                  x.Equals(
                      "letra",
                      StringComparison.OrdinalIgnoreCase) ||
                  x.Equals(
                      "login",
                      StringComparison.OrdinalIgnoreCase) ||
                  x.Equals(
                      "cadastro",
                      StringComparison.OrdinalIgnoreCase)))
      {
        return BadRequest(new
        {
          mensagem =
                "Rota do Cifra Club não permitida."
        });
      }

      // ============================================================
      // RECONSTRÓI A URL NO SERVIDOR.
      //
      // Assim não confiamos integralmente na string enviada
      // pelo navegador.
      // ============================================================

      string urlCifraReal =
          "https://www.cifraclub.com.br/" +
          Uri.EscapeDataString(segmentos[0]) +
          "/" +
          Uri.EscapeDataString(segmentos[1]) +
          "/";

      using var cts =
          new CancellationTokenSource(
              TimeSpan.FromSeconds(25)
          );

      // ============================================================
      // DIRETO PARA O CIFRA CLUB.
      //
      // ZERO TAVILY.
      // ============================================================

      return await ProcessarCifraClubUrl(
          urlCifraReal,
          segmentos[1].Replace("-", " "),
          cts.Token
      );
    }
    catch (OperationCanceledException)
    {
      return StatusCode(408, new
      {
        mensagem =
              "Tempo limite excedido ao carregar a música relacionada."
      });
    }
    catch (Exception ex)
    {
      return StatusCode(500, new
      {
        mensagem =
              "Erro ao carregar a música relacionada.",

        erro =
              ex.Message
      });
    }
  }


  // ====================================================================
  // PROCESSADOR ÚNICO DO CIFRA CLUB
  //
  // Tanto a busca normal quanto a relacionada chegam aqui.
  //
  // IMPORTANTE:
  // Este método NÃO usa Tavily.
  // ====================================================================

  private async Task<IActionResult> ProcessarCifraClubUrl(
    string urlCifraReal,
    string nomeMusicaFallback,
    CancellationToken cancellationToken)
  {
    // ================================================================
    // 1. VALIDA NOVAMENTE A URL
    // ================================================================

    if (!Uri.TryCreate(
            urlCifraReal,
            UriKind.Absolute,
            out var uriPagina))
    {
      return BadRequest(new
      {
        mensagem = "URL da cifra inválida."
      });
    }

    if (!HostCifraClubValido(uriPagina.Host))
    {
      return BadRequest(new
      {
        mensagem = "Domínio da cifra não permitido."
      });
    }

    var segmentosUrl =
        uriPagina.AbsolutePath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries
        );

    if (segmentosUrl.Length != 2)
    {
      return BadRequest(new
      {
        mensagem =
              "A URL não corresponde a uma página de cifra válida."
      });
    }

    // ---------------------------------------------------------------
    // Slugs da página atual
    //
    // Exemplo:
    //
    // segmentosUrl[0] = leandro-e-leonardo
    // segmentosUrl[1] = rumo-goiania
    // ---------------------------------------------------------------

    string artistaSlugAtual =
        segmentosUrl[0];

    string musicaSlugAtual =
        segmentosUrl[1];


    // ================================================================
    // 2. DOWNLOAD DO HTML DA CIFRA
    // ================================================================

    _http.DefaultRequestHeaders.Clear();

    _http.DefaultRequestHeaders.Add(
        "User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/120.0.0.0 Safari/537.36"
    );

    using var respostaPagina =
        await _http.GetAsync(
            urlCifraReal,
            cancellationToken
        );

    if (!respostaPagina.IsSuccessStatusCode)
    {
      return StatusCode(
          (int)respostaPagina.StatusCode,
          new
          {
            mensagem =
                  "Não foi possível carregar a página da cifra."
          }
      );
    }

    var htmlCifra =
        await respostaPagina.Content
            .ReadAsStringAsync(
                cancellationToken
            );

    var docPagina =
        new HtmlDocument();

    docPagina.LoadHtml(
        htmlCifra ?? string.Empty
    );


    // ================================================================
    // 3. CONTAINER PRINCIPAL DA CIFRA
    // ================================================================

    var containerCifra =
        docPagina.DocumentNode.SelectSingleNode(
            "//article[@class='XjgWI']"
        )
        ??
        docPagina.DocumentNode.SelectSingleNode(
            "//pre[contains(@class, '_crVx')]"
        )
        ??
        docPagina.DocumentNode.SelectSingleNode(
            "//pre"
        );

    if (containerCifra == null)
    {
      return Ok(new
      {
        cifraCompleta =
              "Erro: Estrutura da folha de música " +
              "não localizada no Seven Shows."
      });
    }


    // ================================================================
    // 4. ARTISTA
    // ================================================================

    string artistaMapeado =
        artistaSlugAtual
            .Replace("-", " ")
            .ToUpper();


    // ================================================================
    // 5. TÍTULO
    // ================================================================

    var noTitulo =
        docPagina.DocumentNode.SelectSingleNode(
            "//h1"
        );

    string musicaMapeada =
        noTitulo != null
            ? HtmlEntity
                .DeEntitize(
                    noTitulo.InnerText
                )
                .Trim()
                .ToUpper()
            : nomeMusicaFallback
                .ToUpper();


    // ================================================================
    // 6. TOM ORIGINAL
    // ================================================================

    var noDoTom =
        docPagina.DocumentNode.SelectSingleNode(
            "//button[@data-anchor='--chord-tone']"
        )
        ??
        docPagina.DocumentNode.SelectSingleNode(
            "//span[@id='cifra_tom']"
        )
        ??
        docPagina.DocumentNode.SelectSingleNode(
            "//button[contains(@class, 'tom')]"
        );

    string tomOriginalMapeado =
        "B";

    if (noDoTom != null)
    {
      string textoTom =
          HtmlEntity
              .DeEntitize(
                  noDoTom.InnerText
              )
              .Replace("Tom:", "")
              .Trim();

      if (!string.IsNullOrWhiteSpace(
              textoTom))
      {
        tomOriginalMapeado =
            textoTom;
      }
    }


    // ================================================================
    // 7. MÚSICAS RELACIONADAS
    //
    // NÃO FAZ MAIS RASPAGEM DO HTML.
    //
    // API REAL UTILIZADA PELO CIFRA CLUB:
    //
    // /v4/artists/{artistaSlug}/suggested
    // ================================================================

    var listaRelacionadas =
        new List<MusicaRelacionadaDto>();

    try
    {
      string urlSugestoes =
          "https://api.cifraclub.com.br/v4/artists/" +
          Uri.EscapeDataString(
              artistaSlugAtual
          ) +
          "/suggested";

      using var requestSugestoes =
          new HttpRequestMessage(
              HttpMethod.Get,
              urlSugestoes
          );

      requestSugestoes.Headers.TryAddWithoutValidation(
          "User-Agent",
          "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
          "AppleWebKit/537.36 (KHTML, like Gecko) " +
          "Chrome/120.0.0.0 Safari/537.36"
      );

      requestSugestoes.Headers.TryAddWithoutValidation(
          "Accept",
          "application/json"
      );

      using var respostaSugestoes =
          await _http.SendAsync(
              requestSugestoes,
              cancellationToken
          );

      // ------------------------------------------------------------
      // IMPORTANTE:
      //
      // Se a API de sugestões falhar, NÃO derrubamos a cifra.
      // Apenas retornamos relacionadas = [].
      // ------------------------------------------------------------

      if (respostaSugestoes.IsSuccessStatusCode)
      {
        string jsonSugestoes =
            await respostaSugestoes.Content
                .ReadAsStringAsync(
                    cancellationToken
                );

        if (!string.IsNullOrWhiteSpace(
                jsonSugestoes))
        {
          using var docSugestoes =
              JsonDocument.Parse(
                  jsonSugestoes
              );

          if (docSugestoes.RootElement.TryGetProperty(
                  "songs",
                  out var songsElement) &&
              songsElement.ValueKind ==
                  JsonValueKind.Array)
          {
            foreach (
                var song
                in songsElement.EnumerateArray())
            {
              // =================================================
              // NOME DA MÚSICA
              // =================================================

              string relTitulo =
                  string.Empty;

              if (song.TryGetProperty(
                      "name",
                      out var nomeElement) &&
                  nomeElement.ValueKind ==
                      JsonValueKind.String)
              {
                relTitulo =
                    nomeElement.GetString()
                    ?? string.Empty;
              }


              // =================================================
              // SLUG DA MÚSICA
              // =================================================

              string relMusicaSlug =
                  string.Empty;

              if (song.TryGetProperty(
                      "slug",
                      out var slugElement) &&
                  slugElement.ValueKind ==
                      JsonValueKind.String)
              {
                relMusicaSlug =
                    slugElement.GetString()
                    ?? string.Empty;
              }


              // =================================================
              // ARTISTA
              // =================================================

              string relArtista =
                  string.Empty;

              string relArtistaSlug =
                  string.Empty;

              if (song.TryGetProperty(
                      "artist",
                      out var artistaElement) &&
                  artistaElement.ValueKind ==
                      JsonValueKind.Object)
              {
                if (artistaElement.TryGetProperty(
                        "name",
                        out var artistaNomeElement) &&
                    artistaNomeElement.ValueKind ==
                        JsonValueKind.String)
                {
                  relArtista =
                      artistaNomeElement.GetString()
                      ?? string.Empty;
                }

                if (artistaElement.TryGetProperty(
                        "slug",
                        out var artistaSlugElement) &&
                    artistaSlugElement.ValueKind ==
                        JsonValueKind.String)
                {
                  relArtistaSlug =
                      artistaSlugElement.GetString()
                      ?? string.Empty;
                }
              }


              // =================================================
              // LIMPEZA
              // =================================================

              relTitulo =
                  relTitulo.Trim();

              relArtista =
                  relArtista.Trim();

              relMusicaSlug =
                  relMusicaSlug.Trim();

              relArtistaSlug =
                  relArtistaSlug.Trim();


              // =================================================
              // VALIDAÇÃO MÍNIMA
              // =================================================

              if (string.IsNullOrWhiteSpace(
                      relTitulo) ||
                  string.IsNullOrWhiteSpace(
                      relMusicaSlug) ||
                  string.IsNullOrWhiteSpace(
                      relArtistaSlug))
              {
                continue;
              }


              // =================================================
              // MONTA URL REAL DA CIFRA RELACIONADA
              //
              // IMPORTANTE:
              //
              // Usa o artista DA RELACIONADA.
              //
              // Exemplo:
              //
              // zeze-di-camargo-e-luciano
              // +
              // irmao-da-lua-amigo-das-estrelas
              // =================================================

              string relUrlCompleta =
                  "https://www.cifraclub.com.br/" +
                  Uri.EscapeDataString(
                      relArtistaSlug
                  ) +
                  "/" +
                  Uri.EscapeDataString(
                      relMusicaSlug
                  ) +
                  "/";


              // =================================================
              // NÃO ADICIONA A PRÓPRIA MÚSICA
              // =================================================

              bool mesmaMusica =
                  string.Equals(
                      relArtistaSlug,
                      artistaSlugAtual,
                      StringComparison.OrdinalIgnoreCase
                  )
                  &&
                  string.Equals(
                      relMusicaSlug,
                      musicaSlugAtual,
                      StringComparison.OrdinalIgnoreCase
                  );

              if (mesmaMusica)
              {
                continue;
              }


              // =================================================
              // EVITA DUPLICADOS
              // =================================================

              bool jaExiste =
                  listaRelacionadas.Any(
                      x =>
                          string.Equals(
                              x.Url,
                              relUrlCompleta,
                              StringComparison.OrdinalIgnoreCase
                          )
                  );

              if (jaExiste)
              {
                continue;
              }


              // =================================================
              // ADICIONA À LISTA
              // =================================================

              listaRelacionadas.Add(
                  new MusicaRelacionadaDto
                  {
                    Titulo =
                          relTitulo,

                    Artista =
                          string.IsNullOrWhiteSpace(
                              relArtista)
                              ? relArtistaSlug
                                  .Replace("-", " ")
                              : relArtista,

                    TermoBusca =
                          string.IsNullOrWhiteSpace(
                              relArtista)
                              ? relTitulo
                              : $"{relTitulo} {relArtista}",

                    Url =
                          relUrlCompleta
                  }
              );


              // =================================================
              // LIMITE PARA A TELA
              // =================================================

              if (listaRelacionadas.Count >= 12)
              {
                break;
              }
            }
          }
        }
      }
      else
      {
        Console.WriteLine(
            "[CIFRAS] API de relacionadas retornou HTTP " +
            (int)respostaSugestoes.StatusCode +
            " para artista: " +
            artistaSlugAtual
        );
      }
    }
    catch (OperationCanceledException)
        when (!cancellationToken.IsCancellationRequested)
    {
      // ------------------------------------------------------------
      // Timeout específico das relacionadas.
      // Não derruba a cifra principal.
      // ------------------------------------------------------------

      Console.WriteLine(
          "[CIFRAS] Timeout ao carregar músicas relacionadas."
      );
    }
    catch (Exception ex)
    {
      // ------------------------------------------------------------
      // A cifra principal continua funcionando mesmo se o serviço
      // de sugestões estiver indisponível.
      // ------------------------------------------------------------

      Console.WriteLine(
          "[CIFRAS] Não foi possível carregar relacionadas: " +
          ex.Message
      );
    }


    // ================================================================
    // 8. REMOÇÃO CIRÚRGICA DA TABLATURA
    // ================================================================

    var nosDeTablatura =
        containerCifra.SelectNodes(
            ".//div[contains(@class, 'tabs')]"
        )
        ??
        containerCifra.SelectNodes(
            ".//*[contains(@class, 'tabs')]"
        );

    if (nosDeTablatura != null)
    {
      foreach (
          var noTab
          in nosDeTablatura)
      {
        noTab.Remove();
      }
    }


    // ================================================================
    // 9. HTML ESTRUTURADO
    // ================================================================

    var htmlInternoLimpo =
        containerCifra.InnerHtml.Trim();


    // ================================================================
    // 10. EXTRAÇÃO GEOMÉTRICA DA CIFRA
    //
    // ESTA É A PARTE QUE JÁ FICOU CORRETA.
    // NÃO COMPACTAMOS ESPAÇOS.
    // ================================================================

    static string ExtrairLinhaCifra(
        HtmlNode linha)
    {
      var sbLinha =
          new StringBuilder();

      void Percorrer(
          HtmlNode node)
      {
        foreach (
            var filho
            in node.ChildNodes)
        {
          // ----------------------------------------------------
          // TEXTO
          // ----------------------------------------------------

          if (filho.NodeType ==
              HtmlNodeType.Text)
          {
            string texto =
                HtmlEntity.DeEntitize(
                    ((HtmlTextNode)filho).Text
                );

            // NÃO USAR TRIM.
            //
            // Esses espaços são responsáveis pelo
            // posicionamento horizontal dos acordes.
            sbLinha.Append(
                texto
            );

            continue;
          }


          // ----------------------------------------------------
          // QUEBRA INTERNA
          // ----------------------------------------------------

          if (filho.Name.Equals(
                  "br",
                  StringComparison.OrdinalIgnoreCase))
          {
            sbLinha.Append('\n');

            continue;
          }


          // ----------------------------------------------------
          // TAGS INTERNAS
          //
          // Inclui:
          //
          // <b data-chord-name="...">
          // ----------------------------------------------------

          Percorrer(
              filho
          );
        }
      }

      Percorrer(
          linha
      );

      return sbLinha.ToString();
    }


    // ================================================================
    // 11. LOCALIZA AS LINHAS kvMV
    // ================================================================

    var linhasDom =
        containerCifra.SelectNodes(
            ".//div[contains(" +
            "concat(' ', normalize-space(@class), ' '), " +
            "' kvMV '" +
            ")]"
        );

    var linhasTextoFinal =
        new List<string>();


    if (linhasDom != null &&
        linhasDom.Count > 0)
    {
      foreach (
          var linhaDom
          in linhasDom)
      {
        string linha =
            ExtrairLinhaCifra(
                linhaDom
            );


        // --------------------------------------------------------
        // REMOVE SOMENTE LINHAS VERTICAIS VAZIAS.
        //
        // NÃO ALTERA OS ESPAÇOS HORIZONTAIS.
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                linha))
        {
          continue;
        }

        linhasTextoFinal.Add(
            linha
        );
      }
    }
    else
    {
      // ============================================================
      // FALLBACK PARA EVENTUAL ALTERAÇÃO DO DOM
      // ============================================================

      string textoFallback =
          HtmlEntity.DeEntitize(
              containerCifra.InnerText
          );

      var linhasFallback =
          textoFallback.Split(
              new[]
              {
                    "\r\n",
                    "\r",
                    "\n"
              },
              StringSplitOptions.None
          );

      foreach (
          var linha
          in linhasFallback)
      {
        if (string.IsNullOrWhiteSpace(
                linha))
        {
          continue;
        }

        linhasTextoFinal.Add(
            linha
        );
      }
    }


    // ================================================================
    // 12. TEXTO FINAL
    // ================================================================

    var cifraTextoPuroFinal =
        string.Join(
            "\n",
            linhasTextoFinal
        );


    // ================================================================
    // 13. RETORNO PARA O VUE
    // ================================================================

    return Ok(new
    {
      musica =
            musicaMapeada,

      artista =
            artistaMapeado,

      tomOriginal =
            tomOriginalMapeado,

      tomSolicitado =
            tomOriginalMapeado,

      cifraCompleta =
            cifraTextoPuroFinal,

      htmlEstruturado =
            htmlInternoLimpo,

      relacionadas =
            listaRelacionadas
    });
  }


  // ====================================================================
  // VALIDAÇÃO DO HOST DO CIFRA CLUB
  // ====================================================================

  private static bool HostCifraClubValido(
      string host)
  {
    if (string.IsNullOrWhiteSpace(host))
    {
      return false;
    }

    return host.Equals(
               "cifraclub.com.br",
               StringComparison.OrdinalIgnoreCase)
           ||
           host.Equals(
               "www.cifraclub.com.br",
               StringComparison.OrdinalIgnoreCase);
  }

}