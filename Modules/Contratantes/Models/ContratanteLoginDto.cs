using System.ComponentModel.DataAnnotations;

namespace SevenShows.Api.Modules.Contratantes.Models
{
    // ====================================================================
    // 🏛️ DTO DE LOGIN DEDICADO: Modelo de Entrada Isolado para o Contratante
    // ====================================================================
    public class ContratanteLoginDto
    {
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória.")]
        public string Password { get; set; } = string.Empty;
    }
}
