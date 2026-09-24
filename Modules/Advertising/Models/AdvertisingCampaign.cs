using System.ComponentModel.DataAnnotations;

namespace SevenShows.Api.Modules.Advertising.Models;

public class AdvertisingCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(160)] public string AdvertiserName { get; set; } = string.Empty;
    [MaxLength(160)] public string Title { get; set; } = string.Empty;
    [MaxLength(600)] public string? Description { get; set; }
    [MaxLength(255)] public string? DesktopImageUrl { get; set; }
    [MaxLength(255)] public string? MobileImageUrl { get; set; }
    [MaxLength(40)] public string Position { get; set; } = "HOME_PREMIUM";
    [MaxLength(30)] public string DestinationType { get; set; } = "PARTNER_PAGE";
    [MaxLength(500)] public string? DestinationUrl { get; set; }
    [MaxLength(30)] public string? WhatsApp { get; set; }
    [MaxLength(500)] public string? WhatsAppMessage { get; set; }
    [MaxLength(500)] public string? InstagramUrl { get; set; }
    [MaxLength(500)] public string? WebsiteUrl { get; set; }
    [MaxLength(180)] public string? Slug { get; set; }
    [MaxLength(180)] public string? City { get; set; }
    [MaxLength(2)] public string? State { get; set; }
    [MaxLength(255)] public string? LogoUrl { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "ACTIVE";
    public decimal ContractValue { get; set; }
    public long Impressions { get; set; }
    public long Clicks { get; set; }
    public long WhatsAppClicks { get; set; }
    public long InstagramClicks { get; set; }
    public long WebsiteClicks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
