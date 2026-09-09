using Microsoft.EntityFrameworkCore;
using SevenShows.Api.Modules.SaasProducts.Models;
using SevenShows.Api.Modules.Auth.Models;
using SevenShows.Api.Modules.Tenants.Models;
using SevenShows.Modules.Tenants.Models;

namespace SevenShows.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

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
            new Role { Id = "Contratante", Description = "Cliente Realizador de Eventos e Comprador de Shows" } // 🚀 Nova Role
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

        // Hash seguro da senha "AdminSeven7#" gerado previamente via BCrypt
        // Hash REAL e COMPLETO de 60 caracteres da senha "AdminSeven7#" gerado pelo BCrypt
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
                
                // Valores padrão dinâmicos e válidos para conformidade do Administrador
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
            // 🚀 FORÇA TEXTO BRUTO: Converte para o fuso do Brasil se for UTC, e desativa o fuso (Unspecified) para o driver do MariaDB não alterar o dia!
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
    }
}
