using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Contratantes.Models;
using SevenShows.Api.Modules.Contratantes.Domain.Entities;

namespace SevenShows.Api.Modules.Contratantes.Controllers
{
  // ====================================================================
  // 🏛️ CONTROLLER REFATORADO: Gerenciamento Unificado de Contratantes
  // ====================================================================
  [ApiController]
  [Route("api/public/contratantes")]
  public class ContratantesController : ControllerBase
  {
    private readonly AppDbContext _context;

    public ContratantesController(AppDbContext context)
    {
      _context = context;
    }

    [HttpGet("check-email")]
    public async Task<IActionResult> CheckEmail([FromQuery] string email)
    {
      if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "E-mail inválido." });
      var existe = await _context.Contratantes.AnyAsync(c => c.Email.ToLower() == email.Trim().ToLower());
      return Ok(new { exists = existe });
    }

    // 🚀 CADASTRO EM TABELA ÚNICA: Grava e-mail, senha e dados em uma só linha!
    [HttpPost("cadastro-checkout")]
    public async Task<IActionResult> CadastroCheckout([FromBody] CadastroContratanteDto dto)
    {
      if (!ModelState.IsValid) return BadRequest(ModelState);

      if (await _context.Contratantes.AnyAsync(c => c.Email.ToLower() == dto.Email.Trim().ToLower()))
        return BadRequest(new { message = "Este endereço de e-mail já encontra-se cadastrado." });

      using var transacao = await _context.Database.BeginTransactionAsync();
      try
      {
        var novoContratante = new Contratante
        {
          Id = Guid.NewGuid(),
          NomeCompleto = dto.NomeCompleto,
          CPF = dto.CPF.Replace(".", "").Replace("-", ""),
          Celular = dto.Celular,
          Email = dto.Email.Trim().ToLower(),
          PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Senha) // Criptografia Direta
        };
        _context.Contratantes.Add(novoContratante);

        var novoEndereco = new ContratanteAddress
        {
          Id = Guid.NewGuid(),
          ContratanteId = novoContratante.Id,
          ZipCode = dto.ZipCode.Replace("-", ""),
          Logradouro = dto.Logradouro,
          Numero = dto.Numero,
          Bairro = dto.Bairro,
          Cidade = dto.Cidade,
          Estado = dto.Estado,
          Complemento = dto.Complemento
        };
        _context.ContratanteAddresses.Add(novoEndereco);
        await _context.SaveChangesAsync();
        await transacao.CommitAsync();

        // Emissão do Token JWT original de esteira
        var tokenJWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJVc2VySWQiOiI" + novoContratante.Id + "\"}";

        // 🚀 EQUALIZAÇÃO GLOBAL: Entrega o perfil completo hidratado com o endereço recém-criado
        var perfilExpandido = new
        {
          id = novoContratante.Id,
          name = novoContratante.NomeCompleto,
          email = novoContratante.Email,
          cep = novoEndereco.ZipCode ?? "",
          logradouro = novoEndereco.Logradouro ?? "",
          bairro = novoEndereco.Bairro ?? "",
          numero = novoEndereco.Numero ?? "",
          cidade = novoEndereco.Cidade ?? "",
          estado = novoEndereco.Estado ?? "",
          complemento = novoEndereco.Complemento ?? "",
          cpf = novoContratante.CPF,
          celular = novoContratante.Celular
        };

        return Ok(new { success = true, token = tokenJWT, user = perfilExpandido });
      }
      catch (Exception ex)
      {
        await transacao.RollbackAsync();
        return StatusCode(500, new { message = "Erro crítico interno.", error = ex.Message });
      }
    }
    // ====================================================================
    // 🏛️ ROTA: Login Dedicado do Contratante (Validação via Tabela Única)
    // ====================================================================
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] System.Text.Json.JsonElement request)
    {
      try
      {
        // Captura cirúrgica das chaves exatas enviadas pelo Vue 3
        string email = request.GetProperty("email").GetString()?.Trim().ToLower();
        string password = request.GetProperty("password").GetString();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
          return BadRequest(new { message = "E-mail e senha são obrigatórios." });

        // Busca o contratante no MariaDB com base no e-mail higienizado
        var contratante = await _context.Contratantes
            .FirstOrDefaultAsync(c => c.Email.ToLower() == email);

        // Validação idêntica à do seu CadastroCheckout usando a coluna PasswordHash
        if (contratante == null || !BCrypt.Net.BCrypt.Verify(password, contratante.PasswordHash))
          return BadRequest(new { message = "E-mail ou senha incorretos para o perfil Contratante." });

        // Captura o endereço associado para montagem do ecossistema postal no Vue
        var endereco = await _context.ContratanteAddresses
            .FirstOrDefaultAsync(a => a.ContratanteId == contratante.Id);

        // Emissão do seu Token JWT original de esteira
        var tokenJWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJVc2VySWQiOiI" + contratante.Id + "\"}";

        // 🚀 RETORNO INTEGRADO: Entrega o payload completo e limpo para o Step 4
        return Ok(new
        {
          success = true,
          token = tokenJWT,
          user = new
          {
            id = contratante.Id,
            name = contratante.NomeCompleto,
            email = contratante.Email,
            cep = endereco?.ZipCode ?? "",
            logradouro = endereco?.Logradouro ?? "",
            bairro = endereco?.Bairro ?? "",
            numero = endereco?.Numero ?? "",
            cidade = endereco?.Cidade ?? "",
            estado = endereco?.Estado ?? "",
            complemento = endereco?.Complemento ?? "",

            // ⚡ INJETADO: CPF e Celular reais do banco alimentando a tela de faturamento
            cpf = contratante.CPF,
            celular = contratante.Celular
          }
        });
      }
      catch (Exception ex)
      {
        return BadRequest(new { message = "Formato de requisição inválido.", error = ex.Message });
      }
    }

    [HttpPut("atualizar-perfil/{id:guid}")]
    public async Task<IActionResult> AtualizarPerfil([FromRoute] Guid id, [FromBody] System.Text.Json.JsonElement request)
    {
      using var transacao = await _context.Database.BeginTransactionAsync();
      try
      {
        // 1. Extração cirúrgica das strings vindas do formulário Vue 3
        string nome = request.GetProperty("nomeCompleto").GetString()?.Trim();
        string celular = request.GetProperty("celular").GetString()?.Trim();
        string zipCode = request.GetProperty("zipCode").GetString()?.Trim()?.Replace("-", "");
        string logradouro = request.GetProperty("logradouro").GetString()?.Trim();
        string numero = request.GetProperty("numero").GetString()?.Trim();
        string bairro = request.GetProperty("bairro").GetString()?.Trim();
        string cidade = request.GetProperty("cidade").GetString()?.Trim();
        string estado = request.GetProperty("estado").GetString()?.Trim();
        string complemento = request.TryGetProperty("complemento", out var compProp) ? compProp.GetString()?.Trim() : null;

        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(celular))
          return BadRequest(new { success = false, message = "Nome completo e Celular são campos obrigatórios." });

        // 2. BUSCA DO CONTRATANTE PRINCIPAL
        var contratante = await _context.Contratantes.FirstOrDefaultAsync(c => c.Id == id);
        if (contratante == null)
          return NotFound(new { success = false, message = "Cadastro do contratante não localizado no sistema." });

        // Atualiza os dados atômicos do usuário
        contratante.NomeCompleto = nome;
        contratante.Celular = celular;

        // 3. BUSCA OU CRIAÇÃO DO ENDEREÇO ASSOCIADO
        var endereco = await _context.ContratanteAddresses.FirstOrDefaultAsync(a => a.ContratanteId == id);
        if (endereco == null)
        {
          endereco = new ContratanteAddress { Id = Guid.NewGuid(), ContratanteId = id };
          _context.ContratanteAddresses.Add(endereco);
        }

        // Atualiza as colunas postais do rastro global
        endereco.ZipCode = zipCode ?? "";
        endereco.Logradouro = logradouro;
        endereco.Numero = numero;
        endereco.Bairro = bairro;
        endereco.Cidade = cidade;
        endereco.Estado = estado;
        endereco.Complemento = complemento;

        await _context.SaveChangesAsync();
        await transacao.CommitAsync();

        // 4. EMISSÃO DO PAYLOAD DE REIDRATAÇÃO GLOBAL (Mapeamento idêntico ao Login)
        var perfilAtualizado = new
        {
          id = contratante.Id,
          name = contratante.NomeCompleto,
          email = contratante.Email,
          cep = endereco.ZipCode ?? "",
          logradouro = endereco.Logradouro ?? "",
          bairro = endereco.Bairro ?? "",
          numero = endereco.Numero ?? "",
          cidade = endereco.Cidade ?? "",
          estado = endereco.Estado ?? "",
          complemento = endereco.Complemento ?? "",
          cpf = contratante.CPF,
          celular = contratante.Celular
        };

        return Ok(new { success = true, message = "Perfil e rastro de endereço global atualizados com sucesso absoluto!", user = perfilAtualizado });
      }
      catch (Exception ex)
      {
        await transacao.RollbackAsync();
        return StatusCode(500, new { success = false, message = "Erro crítico interno ao atualizar dados cadastrais.", error = ex.Message });
      }
    }

    // ====================================================================
    // 🔒 PUT: Alteração de Senha Segura com Validação Isolada via BCrypt
    // ====================================================================
    [HttpPut("alterar-senha/{id:guid}")]
    public async Task<IActionResult> AlterarSenha([FromRoute] Guid id, [FromBody] System.Text.Json.JsonElement request)
    {
      try
      {
        // 1. Captura cirúrgica das credenciais enviadas pela Aba 4 do Vue 3
        string senhaAtual = request.GetProperty("senhaAtual").GetString();
        string novaSenha = request.GetProperty("novaSenha").GetString();

        if (string.IsNullOrWhiteSpace(senhaAtual) || string.IsNullOrWhiteSpace(novaSenha))
          return BadRequest(new { success = false, message = "A senha atual e a nova senha são obrigatórias." });

        if (novaSenha.Length < 6)
          return BadRequest(new { success = false, message = "A nova senha deve conter no mínimo 6 caracteres." });

        // 2. BUSCA DO CONTRATANTE NO BANCO MARIADB
        var contratante = await _context.Contratantes.FirstOrDefaultAsync(c => c.Id == id);
        if (contratante == null)
          return NotFound(new { success = false, message = "Cadastro do contratante não localizado no sistema." });

        // 3. VALIDAÇÃO DA SENHA ANTIGA CONTRA O PASSWORD_HASH ATUAL
        if (!BCrypt.Net.BCrypt.Verify(senhaAtual, contratante.PasswordHash))
          return BadRequest(new { success = false, message = "A senha atual informada está incorreta." });

        // 4. CRIPTOGRAFIA DA NOVA CREDENCIAL E PERSISTÊNCIA ATÔMICA
        contratante.PasswordHash = BCrypt.Net.BCrypt.HashPassword(novaSenha);
        
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Sua credencial de acesso foi alterada com sucesso absoluto no portal!" });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = "Formato de requisição inválido para alteração de senha.", error = ex.Message });
      }
    }
  }
}

