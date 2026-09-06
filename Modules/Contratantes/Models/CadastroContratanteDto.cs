using System.ComponentModel.DataAnnotations;

namespace SevenShows.Api.Modules.Contratantes.Models
{
    // ====================================================================
    // 🏛️ DATA TRANSFER OBJECT (DTO): Payload Unificado de Cadastro e Rota
    // ====================================================================
    public class CadastroContratanteDto
    {
        // --- 📋 DADOS DE IDENTIFICAÇÃO E AUTENTICAÇÃO (TABELA CONTRATANTES) ---
        [Required(ErrorMessage = "O nome completo é obrigatório.")]
        [StringLength(150, ErrorMessage = "O nome não pode exceder 150 caracteres.")]
        public string NomeCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF é obrigatório.")]
        [StringLength(14, ErrorMessage = "O CPF inválido.")]
        public string CPF { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail de acesso é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "O celular é obrigatório.")]
        public string Celular { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha de acesso é obrigatória.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve conter no mínimo 6 caracteres.")]
        public string Senha { get; set; } = string.Empty;

        // --- 🏠 DADOS POSTAIS DO LOGÍSTICA DO SHOW (TABELA CONTRATANTEADDRESSES) ---
        [Required(ErrorMessage = "O CEP é obrigatório.")]
        public string ZipCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "O logradouro é obrigatório.")]
        public string Logradouro { get; set; } = string.Empty;

        [Required(ErrorMessage = "O número é obrigatório.")]
        public string Numero { get; set; } = string.Empty;

        [Required(ErrorMessage = "O bairro é obrigatório.")]
        public string Bairro { get; set; } = string.Empty;

        [Required(ErrorMessage = "A cidade é obrigatória.")]
        public string Cidade { get; set; } = string.Empty;

        [Required(ErrorMessage = "O estado (UF) é obrigatório.")]
        public string Estado { get; set; } = string.Empty;

        public string? Complemento { get; set; }
    }
}
