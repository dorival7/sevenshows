using System.ComponentModel.DataAnnotations;

namespace SevenShows.Api.Modules.Tenants.Pagamentos.Dtos
{
    // ====================================================================
    // 💳 DTO COMPLETO: Transporta os dados do cartão e a tipagem do titular
    // ====================================================================
    public class ProcessarPagamentoCartaoDto
    {
        [Required(ErrorMessage = "O identificador do evento é obrigatório.")]
        public string ArtistEventId { get; set; } = string.Empty;

        [Required(ErrorMessage = "O identificador do contratante é obrigatório.")]
        public string ContratanteId { get; set; } = string.Empty;

        [Required(ErrorMessage = "O número do cartão de crédito é obrigatório.")]
        [CreditCard(ErrorMessage = "Formato de número de cartão inválido.")]
        public string CartaoNumero { get; set; } = string.Empty;

        [Required(ErrorMessage = "O mês de validade é obrigatório.")]
        public string CartaoValidadeMes { get; set; } = string.Empty;

        [Required(ErrorMessage = "O ano de validade é obrigatório.")]
        public string CartaoValidadeAno { get; set; } = string.Empty;

        [Required(ErrorMessage = "O código de segurança CVC é obrigatório.")]
        public string CartaoCvc { get; set; } = string.Empty;

        [Required(ErrorMessage = "Os dados cadastrais do titular são obrigatórios.")]
        public HolderCardInfoObjeto HolderInfo { get; set; } = new();
    }

    // 🚀 CLASSE AUXILIAR: Resolve o erro CS0246 mapeando a qualificação civil do titular
    public class HolderCardInfoObjeto
    {
        [Required(ErrorMessage = "O nome impresso no cartão é obrigatório.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF ou CNPJ do titular é obrigatório.")]
        public string CpfCnpj { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail do titular do cartão é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "O celular do titular do cartão é obrigatório.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CEP de faturamento do titular é obrigatório.")]
        public string PostalCode { get; set; } = string.Empty;
    }
}
