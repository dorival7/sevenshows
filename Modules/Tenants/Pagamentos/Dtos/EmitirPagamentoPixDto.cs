using System.ComponentModel.DataAnnotations;

namespace SevenShows.Api.Modules.Tenants.Pagamentos.Dtos
{
    // ====================================================================
    // 📱 DTO ENXUTO: Valida unicamente a esteira de faturamento via Pix
    // ====================================================================
    public class EmitirPagamentoPixDto
    {
        [Required(ErrorMessage = "O identificador do evento é obrigatório.")]
        public string ArtistEventId { get; set; } = string.Empty;

        [Required(ErrorMessage = "O identificador do contratante é obrigatório.")]
        public string ContratanteId { get; set; } = string.Empty;
    }
}
