using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Modules.SaasProducts.Models;
using SevenShows.Api.Modules.Auth.Models;
using SevenShows.Api.Modules.Tenants.Models;
using SevenShows.Modules.Tenants.Models;
using SevenShows.Api.Modules.Tenants.Repertorios.Models;

namespace SevenShows.Api.Data;

// 📋 Classes utilitárias posicionadas corretamente dentro do namespace para o parsing dos seus JSONs
public class JsonEstadoDto { public int codigo_uf { get; set; } public string uf { get; set; } = string.Empty; }
public class JsonMunicipioDto { public string nome { get; set; } = string.Empty; public decimal latitude { get; set; } public decimal longitude { get; set; } public int codigo_uf { get; set; } }

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DesignerAsset> DesignerAssets { get; set; } = default!;
    public DbSet<Repertorio> Repertorios { get; set; } = default!;
    public DbSet<RepertorioMusica> RepertorioMusicas { get; set; } = default!;
    public DbSet<DesignerPoster> DesignerPosters { get; set; } = default!;
    
    public DbSet<CoordenadasMunicipio> CoordenadasMunicipios { get; set; }
    public DbSet<SaaSInvoice> SaaSInvoices { get; set; }
    public DbSet<ArtistAvailability> ArtistAvailabilities { get; set; } = default!;
    public DbSet<ArtistAgendaBlock> ArtistAgendaBlocks { get; set; } = default!;
    public DbSet<SaaSSubscription> SaaSSubscriptions { get; set; } = default!;
    public DbSet<ArtistAddress> ArtistAddresses { get; set; } = default!;
    public DbSet<ArtistMedia> ArtistMedias { get; set; } = default!;
    public DbSet<ArtistComercialSetting> ArtistComercialSettings { get; set; } = default!;
    public DbSet<ArtistPackage> ArtistPackages { get; set; } = default!;
    public DbSet<ArtistEvent> ArtistEvents { get; set; } = default!;
    public DbSet<ArtistWalletTransaction> ArtistWalletTransactions { get; set; } = default!;
    public DbSet<SaaSPlan> SaaSPlans { get; set; } = default!;
    public DbSet<User> Users { get; set; } = default!;
    public DbSet<Role> Roles { get; set; } = default!;

    // ====================================================================
    // 🏛️ DBSETS ADICIONADOS: Exposição do Ecossistema Isolado do Contratante
    // ====================================================================
    public DbSet<SevenShows.Api.Modules.Contratantes.Domain.Entities.Contratante> Contratantes { get; set; } = default!;
    public DbSet<SevenShows.Api.Modules.Contratantes.Domain.Entities.ContratanteAddress> ContratanteAddresses { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // 1. Mantém as regras e propriedades originais do seu SaaSPlan intactas
        modelBuilder.Entity<SaaSPlan>(entity =>
        {
            entity.Property(p => p.MonthlyFee)
                  .HasColumnType("decimal(18,2)");

            entity.Property(p => p.DefaultTakeRatePercent)
                  .HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<Repertorio>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.UpdatedAt });
            entity.HasMany(x => x.Musicas)
                  .WithOne(x => x.Repertorio)
                  .HasForeignKey(x => x.RepertorioId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RepertorioMusica>(entity =>
        {
            entity.Property(x => x.CifraCompleta).HasColumnType("longtext");
            entity.Property(x => x.HtmlEstruturado).HasColumnType("longtext");
            entity.HasIndex(x => new { x.RepertorioId, x.Ordem });
        });

        modelBuilder.Entity<DesignerPoster>(entity =>
        {
            entity.HasIndex(p => new { p.UserId, p.EventId }).IsUnique();
            entity.Property(p => p.StateJson).HasColumnType("longtext");
            entity.HasIndex(p => new { p.UserId, p.IsDraft });
            entity.HasIndex(p => new { p.UserId, p.IsActive });
            entity.HasIndex(p => p.UpdatedAt);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.IncomeValue).HasColumnType("decimal(18,2)");

            // Mapeamentos específicos para as colunas do MariaDB
            entity.Property(u => u.EstiloMusical).HasMaxLength(50);
            entity.Property(u => u.FormatoArtístico).HasMaxLength(50);
            entity.Property(u => u.Slogan).HasMaxLength(255);

            // Força a criação do tipo LONGTEXT físico no MariaDB
            entity.Property(u => u.Biografia).HasColumnType("longtext");
            entity.Property(u => u.Slug).HasMaxLength(100);

            // Cria o índice no banco para buscas ultrarrápidas por URL
            entity.HasIndex(u => u.Slug).HasDatabaseName("IX_users_Slug");
        });

        // 2. Configura a tabela intermediária many-to-many para as permissões
        modelBuilder.Entity<User>()
            .HasMany(u => u.Roles)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "user_roles",
                j => j.HasOne<Role>().WithMany().HasForeignKey("role_id"),
                j => j.HasOne<User>().WithMany().HasForeignKey("user_id")
            );

        // 3. SEED: Insere as cargas iniciais obrigatórias de perfis no banco
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = "SuperAdmin", Description = "Administrador Global do SaaS" },
            new Role { Id = "Tenant", Description = "Musico, Banda ou Agencia Parceira" },
            new Role { Id = "Contratante", Description = "Cliente Realizador de Eventos e Comprador de Shows" }
        );

        modelBuilder.Entity<ArtistComercialSetting>(entity =>
        {
            entity.Property(p => p.ExtraKmValue).HasColumnType("decimal(18,2)");
            entity.Property(p => p.ExtraHourValue).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ArtistPackage>(entity =>
        {
            entity.Property(p => p.BasePrice).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ArtistWalletTransaction>(entity =>
        {
            entity.Property(t => t.Value).HasColumnType("decimal(18,2)");
        });

        // IDs fixos para o Seed garantir o relacionamento perfeito na tabela intermediária
        var adminUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var senhaCriptografada = "$2a$11$Ff1m9XoBw9Rk7oI8PqZ8Ue4W5L4z8PqZ8Ue4W5L4z8PqZ8Ue4W5L4";

        // SEED: Cadastra o primeiro usuário Administrador do SevenShows
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = adminUserId,
                Name = "Administrador SevenShows",
                Email = "admin@sevenshows.com.br",
                PasswordHash = senhaCriptografada,
                PersonType = "Legal",
                Cpf = "00000000000",
                Cnpj = "00000000000100",
                SubscriptionStatus = "Active",
                ProfileStatus = "Active",
                BirthDate = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                MobilePhone = "11999999999",
                IncomeValue = 0.00m,
                CompanyType = "LTDA",
                AsaasAccountStatus = "APPROVED"
            }
        );

        // 5. SEED: Vincula o usuário Administrador ao perfil 'SuperAdmin' na tabela user_roles
        modelBuilder.Entity("user_roles").HasData(
            new { user_id = adminUserId, role_id = "SuperAdmin" }
        );

        var timezoneId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? "E. South America Standard Time"
            : "America/Sao_Paulo";

        var brZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);

        var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v.Kind == DateTimeKind.Utc ? TimeZoneInfo.ConvertTimeFromUtc(v, brZone) : v, DateTimeKind.Unspecified),
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified)
        );

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
            }
        }

        // ====================================================================
        // 🌍 SEED GEOGRÁFICO NACIONAL AUTOMATIZADO COM CRUZAMENTO DE CÓDIGO/UF
        // ====================================================================
        modelBuilder.Entity<CoordenadasMunicipio>();

        string pastaSeeds = Path.Combine(AppContext.BaseDirectory, "Data", "Seeds");
        string caminhoMunicipios = Path.Combine(pastaSeeds, "municipios.json");
        string caminhoEstados = Path.Combine(pastaSeeds, "estados.json");

        if (File.Exists(caminhoMunicipios) && File.Exists(caminhoEstados))
        {
            try
            {
                string jsonEstados = File.ReadAllText(caminhoEstados);
                var listaEstados = System.Text.Json.JsonSerializer.Deserialize<List<JsonEstadoDto>>(jsonEstados);
                var dicionarioUfs = listaEstados?.ToDictionary(e => e.codigo_uf, e => e.uf.ToUpper().Trim()) ?? new Dictionary<int, string>();

                string jsonMunicipios = File.ReadAllText(caminhoMunicipios);
                var listaMunicipiosRaw = System.Text.Json.JsonSerializer.Deserialize<List<JsonMunicipioDto>>(jsonMunicipios);

                if (listaMunicipiosRaw != null && listaMunicipiosRaw.Count > 0)
                {
                    var registrosSeed = new List<CoordenadasMunicipio>();
                    int idSequencial = 1;

                    foreach (var munRaw in listaMunicipiosRaw)
                    {
                        if (!dicionarioUfs.TryGetValue(munRaw.codigo_uf, out string? siglaUf))
                        {
                            continue;
                        }

                        registrosSeed.Add(new CoordenadasMunicipio
                        {
                            Id = idSequencial++,
                            Uf = siglaUf,
                            NomeCidade = munRaw.nome.Trim(),
                            Latitude = munRaw.latitude,
                            Longitude = munRaw.longitude
                        });
                    }

                    modelBuilder.Entity<CoordenadasMunicipio>().HasData(registrosSeed);
                    Console.WriteLine($"🎰 [DATABASE SEED] {registrosSeed.Count} municípios brasileiros mapeados com sucesso.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [DATABASE SEED] Falha ao cruzar os JSONs de geo-localização: {ex.Message}");
            }
        }
    }
}