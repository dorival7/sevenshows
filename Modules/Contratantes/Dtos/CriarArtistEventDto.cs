using System.ComponentModel.DataAnnotations;

namespace SevenShows.Api.Modules.Contratantes.Dtos
{
    // ====================================================================
    // 📝 DTO CORRIGIDO: Remove a obrigação do UserId no payload de entrada
    // ====================================================================
    public class CriarArtistEventDto
    {
        // 🚀 Removido o [Required] para permitir que o .NET busque o músico na tabela de pacotes
        public string? UserId { get; set; } 

        [Required(ErrorMessage = "O identificador do contratante logado é obrigatório.")]
        public string ContractorId { get; set; } = string.Empty; 

        [Required(ErrorMessage = "O identificador do pacote comercial é obrigatório.")]
        public Guid? ArtistPackageId { get; set; }

        [Required(ErrorMessage = "O título do evento é obrigatório.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data e hora do evento são obrigatórias.")]
        public DateTime EventDate { get; set; }

        [Required(ErrorMessage = "O nome do local do evento é obrigatório.")]
        public string VenueName { get; set; } = string.Empty;

        [Required(ErrorMessage = "A cidade do evento é obrigatória.")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "O estado (UF) do evento é obrigatório.")]
        public string State { get; set; } = string.Empty;

        public string? ContractorName { get; set; }
        public string? EventType { get; set; }
        public decimal BasePackagePrice { get; set; }
        public int DistanceKm { get; set; }
        public int ExtraKm { get; set; }
        public decimal ExtraKmValueCharged { get; set; }
        public int RequestedDurationHours { get; set; }
        public int ExtraHours { get; set; }
        public decimal ExtraHoursValueCharged { get; set; }
        public decimal TotalProposedPrice { get; set; }
        public string? Notes { get; set; }
    }
}
