using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenShows.Modules.Tenants.Models
{
    [Table("saas_invoices")]
    public class SaaSInvoice
    {
        [Key]
        [Column(TypeName = "char(36)")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column(TypeName = "char(36)")]
        public Guid UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string AsaasInvoiceId { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "PENDING"; // PENDING, PAYMENT_RECEIVED, PAYMENT_OVERDUE

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = "PIX"; // PIX, CREDIT_CARD

        [Column(TypeName = "text")]
        public string? PixCopyPaste { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
