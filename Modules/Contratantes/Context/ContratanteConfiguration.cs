using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SevenShows.Api.Modules.Contratantes.Domain.Entities;

namespace SevenShows.Api.Modules.Contratantes.Context
{
    // ====================================================================
    // 🏛️ FLUENT API ATUALIZADA: Mapeamento da Tabela Única de Contratantes
    // ====================================================================
    public class ContratanteConfiguration : IEntityTypeConfiguration<Contratante>
    {
        public void Configure(EntityTypeBuilder<Contratante> builder)
        {
            builder.ToTable("contratantes");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.NomeCompleto)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(c => c.CPF)
                .IsRequired()
                .HasMaxLength(14);

            builder.Property(c => c.Celular)
                .IsRequired()
                .HasMaxLength(20);

            // 🚀 MAPEAMENTO DO ACESSO: Campos de login movidos para a tabela de domínio
            builder.Property(c => c.Email)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(c => c.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(c => c.LogoUrl)
                .HasMaxLength(500);

            // INDEXADOR DE PERFORMANCE: Blinda a integridade forçando e-mails únicos na base
            builder.HasIndex(c => c.Email)
                .IsUnique();

            // Configuração do Relacionamento 1-para-Muitos (Addresses) com Deleção em Cascata
            builder.HasMany(c => c.Addresses)
                .WithOne(a => a.Contratante)
                .HasForeignKey(a => a.ContratanteId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
