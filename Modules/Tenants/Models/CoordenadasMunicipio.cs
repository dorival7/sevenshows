using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SevenShows.Api.Modules.Tenants.Models;

[Table("CoordenadasMunicipios")]
[Index(nameof(Uf))] 
[Index(nameof(NomeCidade))] 
public class CoordenadasMunicipio
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(2)]
    public string Uf { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string NomeCidade { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,8)")]
    public decimal Latitude { get; set; }

    [Column(TypeName = "decimal(11,8)")]
    public decimal Longitude { get; set; }
}
