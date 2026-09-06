using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SevenShows.Api.Modules.Contratantes.Domain.Entities;

namespace SevenShows.Api.Modules.Contratantes.Context
{
    // ====================================================================
    // 🏛️ FLUENT API: Configuração e Limites da Tabela ContratanteAddresses no MariaDB
    // ====================================================================
    public class ContratanteAddressConfiguration : IEntityTypeConfiguration<ContratanteAddress>
    {
        public void Configure(EntityTypeBuilder<ContratanteAddress> builder)
        {
            builder.ToTable("contratante_addresses");

            builder.HasKey(a => a.Id);

            // Mapeamento dos tamanhos estritos das colunas de logística (ViaCEP)
            builder.Property(a => a.ZipCode)
                .IsRequired()
                .HasMaxLength(9); // Suporta máscara 00000-000

            builder.Property(a => a.Logradouro)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(a => a.Numero)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(a => a.Bairro)
                .IsRequired()
                .HasMaxLength(80);

            builder.Property(a => a.Cidade)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(a => a.Estado)
                .IsRequired()
                .HasMaxLength(2); // Guarda apenas a UF (ex: PR)

            builder.Property(a => a.Complemento)
                .HasMaxLength(100)
                .IsRequired(false); // Campo totalmente opcional

            // O relacionamento com o pai já foi amarrado na classe ContratanteConfiguration,
            // mas declaramos a FK de forma explícita aqui para blindar a integridade
            builder.HasOne(a => a.Contratante)
                .WithMany(c => c.Addresses)
                .HasForeignKey(a => a.ContratanteId);
        }
    }
}
