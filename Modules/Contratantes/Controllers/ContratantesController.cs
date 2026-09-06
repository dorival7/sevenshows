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

        var tokenJWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJVc2VySWQiOiI" + novoContratante.Id + "\"}";
        var perfil = new { id = novoContratante.Id, name = novoContratante.NomeCompleto, email = novoContratante.Email };

        return Ok(new { success = true, token = tokenJWT, user = perfil });
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
  }
}

