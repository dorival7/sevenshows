using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SevenShows.Api.Modules.Auth.Models;

[Table("roles")]
public class Role
{
    [Key]
    [Required]
    [StringLength(50)]
    public string Id { get; set; } = string.Empty; // Ex: "SuperAdmin", "Tenant"

    [Required]
    [StringLength(255)]
    public string Description { get; set; } = string.Empty;
}
