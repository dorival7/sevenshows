namespace SevenShows.Api.Modules.SaasProducts.Models;

public class SaaSPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal MonthlyFee { get; set; }
    public decimal DefaultTakeRatePercent { get; set; }
    
    // Limites de recursos controlados dinamicamente pelo banco de dados
    public int MaxShowsPerMonth { get; set; }
    public int MaxPhotosCount { get; set; }
    public int MaxVideosCount { get; set; }
    public bool IsActive { get; set; }

    // NOVO: Período de vigência da assinatura em meses (Padrão 12)
    public int DurationMonths { get; set; } = 12;

    // Relacionamento reverso com a classe User mapeada no seu projeto
    public List<SevenShows.Api.Modules.Auth.Models.User> Users { get; set; } = new();

    // Construtor padrão
    public SaaSPlan()
    {
        Id = Guid.NewGuid();
        IsActive = true;
    }
}
