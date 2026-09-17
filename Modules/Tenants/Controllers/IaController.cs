using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

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
  public async Task<ActionResult> OptimizeSetlistReal([FromBody] OptimizeSetlistRequest request)
  {
    if (request == null || request.Estilos == null || request.Estilos.Count == 0 || string.IsNullOrWhiteSpace(request.PerfilPublico))
    {
      return BadRequest(new { mensagem = "Os parâmetros 'estilos', 'perfilPublico' e 'qtdMusicas' são obrigatórios." });
    }

    string? groqBaseUrl = _configuration["Groq:BaseUrl"];
    string? groqApiKey = _configuration["Groq:ApiKey"];

    if (string.IsNullOrWhiteSpace(groqBaseUrl) || string.IsNullOrWhiteSpace(groqApiKey))
    {
      return StatusCode(500, new { mensagem = "Configuração Groq ausente no appsettings.json." });
    }

    string urlFinal = groqBaseUrl.EndsWith("/")
        ? $"{groqBaseUrl}chat/completions"
        : $"{groqBaseUrl}/chat/completions";

    _http.DefaultRequestHeaders.Clear();
    _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {groqApiKey.Trim()}");

    string estilosTexto = string.Join(", ", request.Estilos);

    // 🧠 AJUSTADO: Instruído explicitamente o tamanho curto de justificativa para economizar payload e tokens
    var promptSistema = @"Você é um Diretor Musical sênior especializado em setlists.
        REGRAS OBRIGATÓRIAS:
        1. Responda APENAS um objeto JSON válido.
        2. Nenhum texto antes ou depois do JSON.
        3. Sem comentários, sem markdown, sem explicações.
        4. Seja extremamente conciso, direto e objetivo nas frases de 'justificativaIA' (máximo 12 palavras).
        5. Siga EXATAMENTE este formato:
        {
          ""resumoEstrategico"": ""texto aqui"",
          ""sugestaoSetlist"": [
            {
              ""ordem"": 1,
              ""titulo"": ""Nome da Música"",
              ""artistaOriginal"": ""Nome do Artista"",
              ""curvaEnergia"": ""Alta"",
              ""justificativaIA"": ""frase curta de marketing aqui""
            }
          ]
        }";

    var promptUsuario = $"Gere um setlist com exatamente {request.QtdMusicas} músicas, mesclando os estilos: {estilosTexto}. " +
                        $"Público/Evento: {request.PerfilPublico}. " +
                        $"Ordene por curva de energia: início instigante, meio estável, final explosivo.";

    // 🎯 ATUALIZADO: maxTokens expandido para 3000 para garantir que caiba shows grandes de 20 a 30 músicas sem cortar
    var requestBody = new GroqRequest(
        model: "openai/gpt-oss-120b",
        messages: new[]
        {
                new ChatMessage("system", promptSistema),
                new ChatMessage("user", promptUsuario)
        },
        responseFormat: new GroqResponseFormat("json_object"),
        maxTokens: 3000,
        temperature: 0.3
    );

    string json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    try
    {
      var resposta = await _http.PostAsync(urlFinal, content);
      var corpo = await resposta.Content.ReadAsStringAsync();

      if (!resposta.IsSuccessStatusCode)
      {
        return StatusCode((int)resposta.StatusCode, new
        {
          mensagem = "Falha ao contatar IA",
          detalhes = corpo
        });
      }

      var groqResp = JsonSerializer.Deserialize<GroqResponse>(corpo);
      if (groqResp?.Choices == null || groqResp.Choices.Length == 0)
      {
        return StatusCode(500, new { mensagem = "A IA não retornou sugestões." });
      }

      string? conteudoJson = groqResp.Choices[0].Message.GetProperty("content").GetString();
      if (string.IsNullOrWhiteSpace(conteudoJson))
      {
        return StatusCode(500, new { mensagem = "Resposta vazia da IA." });
      }

      var resultado = JsonSerializer.Deserialize<JsonElement>(conteudoJson);
      return Ok(resultado);
    }
    catch (Exception ex)
    {
      return StatusCode(500, new { mensagem = "Erro interno", erro = ex.Message });
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