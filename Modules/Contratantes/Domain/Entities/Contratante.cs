using System;
using System.Collections.Generic;

namespace SevenShows.Api.Modules.Contratantes.Domain.Entities
{
    // ====================================================================
    // 🏛️ ENTIDADE DE DOMÍNIO ATUALIZADA: Contratante (Tabela Única Autônoma)
    // ====================================================================
    public class Contratante
    {
        public Guid Id { get; set; }
        
        public string NomeCompleto { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string Celular { get; set; } = string.Empty;
        
        // 🚀 INJETADO: E-mail e Senha morando na mesma tabela de domínio comercial
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // Logo opcional do contratante/estabelecimento.
        public string? LogoUrl { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Propriedade de Navegação Relacional (1 para Muitos Endereços)
        public virtual ICollection<ContratanteAddress> Addresses { get; set; } = new List<ContratanteAddress>();
    }
}
