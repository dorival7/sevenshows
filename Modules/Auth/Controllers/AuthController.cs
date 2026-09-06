using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Auth.Services;

namespace SevenShows.Api.Modules.Auth.Controllers;

public record LoginRequest(string Email, string Password);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;

    public AuthController(AppDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        // ==========================================
        // GATILHO TEMPORÁRIO DE CORREÇÃO DO ADMIN
        // ==========================================
        if (request.Email == "admin@sevenshows.com.br" && request.Password == "AdminSeven7#")
        {
            // 1. Busca se o usuário admin já existe no MySQL
            var adminUser = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (adminUser != null)
            {
                // 2. Sobrescreve a senha hash velha no banco gerando uma nova idêntica em tempo de execução
                adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminSeven7#");
                await _context.SaveChangesAsync();

                // 3. Libera o Token JWT imediatamente sem passar pelo fluxo antigo
                var rolesAdmin = adminUser.Roles.Select(r => r.Id).ToList();
                var tokenAdmin = _tokenService.CriarToken(adminUser.Id, adminUser.Name, adminUser.Email, rolesAdmin);

                return Ok(new { token = tokenAdmin, name = adminUser.Name, roles = rolesAdmin });
            }
        }
        // ==========================================

        // Fluxo padrão para os outros usuários do sistema (Mantido intocado)
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            return Unauthorized(new { message = "E-mail ou senha incorretos." });

        bool senhaValida = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!senhaValida)
            return Unauthorized(new { message = "E-mail ou senha incorretos." });

        var rolesDoUsuario = user.Roles.Select(r => r.Id).ToList();
        var token = _tokenService.CriarToken(user.Id, user.Name, user.Email, rolesDoUsuario);

        return Ok(new { token, name = user.Name, roles = rolesDoUsuario });
    }
}
