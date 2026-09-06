using System;

namespace SevenShows.Api.Modules.Tenants.Models;

public class ArtistWalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Type { get; set; } = "Receivable"; // Receivable, Payout
    public decimal Value { get; set; }
    public bool IsReleased { get; set; } = false; // False = A Receber, True = Sacado
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
