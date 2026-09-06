using System;

namespace SevenShows.Api.Modules.Contratantes.Domain.Entities
{
    // ====================================================================
    // 🏛️ ENTIDADE DE DOMÍNIO: ContratanteAddress (Histórico de Locais de Shows)
    // ====================================================================
    public class ContratanteAddress
    {
        public Guid Id { get; set; }
        
        // Chave Estrangeira e Propriedade de Navegação para o Cliente Pai
        public Guid ContratanteId { get; set; }
        public virtual Contratante Contratante { get; set; } = null!;

        // Campos de Logística Postal herdados do fluxo ViaCEP do Vue 3
        public string ZipCode { get; set; } = string.Empty;
        public string Logradouro { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Bairro { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty; // UF (ex: PR)
        public string? Complemento { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
