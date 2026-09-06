using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SevenShows.Api.Modules.Auth.Models;
using SevenShows.Api.Modules.SaasProducts.Models;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("saas_subscriptions")]
public class SaaSSubscription
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    public Guid SaaSPlanId { get; set; }

    [ForeignKey(nameof(SaaSPlanId))]
    public SaaSPlan? SaaSPlan { get; set; }

    [Required]
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime EndDate { get; set; }

    // ID único do contrato retornado do painel do Asaas (ex: sub_xyz123)
    [StringLength(100)]
    public string? AsaasSubscriptionId { get; set; }

    // Token devolvido pelo Asaas para cobrar os meses futuros no cartão automaticamente
    [StringLength(255)]
    public string? CreditCardToken { get; set; }

    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; } = "PIX"; // "PIX" ou "CREDIT_CARD"

    // Estados: "Pending", "Active", "PastDue", "Canceled"
    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SaaSSubscription()
    {
        Id = Guid.NewGuid();
    }
}
