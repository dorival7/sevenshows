using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.SaasProducts.Models;
using SevenShows.Api.Modules.Auth.Models; // GARANTE A LEITURA DA CLASSE ROLE

namespace SevenShows.Api.Modules.Auth.Models;

[Table("users")]
public class User
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string PersonType { get; set; } = "Physical"; // "Physical" ou "Legal"

    [Required]
    [StringLength(14)]
    public string Cpf { get; set; } = string.Empty;

    [StringLength(18)]
    public string? Cnpj { get; set; }

    [Required]
    [StringLength(50)]
    public string SubscriptionStatus { get; set; } = "Pending";

    [Required]
    [StringLength(50)]
    public string ProfileStatus { get; set; } = "Incomplete_Logistics";

    public Guid? SaaSPlanId { get; set; }

    [ForeignKey(nameof(SaaSPlanId))]
    public SaaSPlan? CurrentPlan { get; set; }

    // ====================================================================
    // NOVOS CAMPOS REAIS DE COMPLIANCE E CARTEIRA (ETAPA 6)
    // ====================================================================
    [Required]
    [StringLength(150)]
    public string ResponsibleName { get; set; } = string.Empty;
    
    [Required]
    public DateTime BirthDate { get; set; }

    [Required]
    [StringLength(20)]
    public string MobilePhone { get; set; } = string.Empty;

    [Required]
    public decimal IncomeValue { get; set; }

    [StringLength(30)]
    public string? CompanyType { get; set; } // MEI, INDIVIDUAL, LTDA, SA, etc.

    [StringLength(100)]
    public string? AsaasWalletId { get; set; }

    [StringLength(255)]
    public string? AsaasOnboardingUrl { get; set; }

    [Required]
    [StringLength(30)]
    public string AsaasAccountStatus { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED

    // 🆕 ADICIONADO: Propriedades de Marketing e Identidade Visual para o Portal Público da SevenShows
    [StringLength(50)]
    public string? EstiloMusical { get; set; } // Ex: Sertanejo, Rock, Pagode

    [StringLength(50)]
    public string? FormatoArtístico { get; set; } // Ex: Banda, Dupla, Solo

    [StringLength(255)]
    public string? Slogan { get; set; } // A frase de impacto de duas linhas da galeria

    public string? Biografia { get; set; } // Texto longo de trajetória do artista (longtext no banco)

    [StringLength(100)]
    public string? Slug { get; set; } // Link amigável (Ex: banda-dois)

    // ====================================================================
    // RELACIONAMENTO MUITOS-PARA-MUITOS ORIGINAL PRESERVADO
    // ====================================================================
    public List<Role> Roles { get; set; } = new();

    public User()
    {
        Id = Guid.NewGuid();
    }

    // 🛠️ ENGENHARIA DE URL: Gera slugs amigáveis automaticamente (Ex: "Banda Dois!" vira "banda-dois")
    public void GerarSlugNativo()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        string texto = Name.ToLower().Trim();
        
        // Substitui caracteres acentuados comuns do português
        string comAcento = "áàâãäéèêëíìîïóòôõöúùûüçñ";
        string semAcento = "aaaaaeeeeiiiiooooouuuucn";
        for (int i = 0; i < comAcento.Length; i++)
        {
            texto = texto.Replace(comAcento[i], semAcento[i]);
        }

        // Remove caracteres especiais e transforma espaços em hífens
        System.Text.StringBuilder sb = new();
        foreach (char c in texto)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == ' ' || c == '-') sb.Append('-');
        }

        string resultado = sb.ToString();
        while (resultado.Contains("--")) resultado = resultado.Replace("--", "-");
        
        Slug = resultado.Trim('-');
    }
}
