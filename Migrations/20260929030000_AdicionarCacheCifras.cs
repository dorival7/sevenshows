using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using SevenShows.Api.Data;

#nullable disable

namespace SevenShows.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260929030000_AdicionarCacheCifras")]
    public partial class AdicionarCacheCifras : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cifras_cache",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    NomeMusica = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    NomeMusicaNormalizado = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    NomeArtista = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    NomeArtistaNormalizado = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    TomOriginal = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CifraCompleta = table.Column<string>(type: "longtext", nullable: false),
                    HtmlEstruturado = table.Column<string>(type: "longtext", nullable: false),
                    RelacionadasJson = table.Column<string>(type: "longtext", nullable: false),
                    SourceUrl = table.Column<string>(type: "varchar(700)", maxLength: 700, nullable: false),
                    FormatoVersao = table.Column<int>(type: "int", nullable: false),
                    SearchCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastAccessAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cifras_cache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cifras_cache_NomeMusicaNormalizado",
                table: "cifras_cache",
                column: "NomeMusicaNormalizado");

            migrationBuilder.CreateIndex(
                name: "IX_cifras_cache_NomeMusicaNormalizado_NomeArtistaNormalizado",
                table: "cifras_cache",
                columns: new[] { "NomeMusicaNormalizado", "NomeArtistaNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cifras_cache_SourceUrl",
                table: "cifras_cache",
                column: "SourceUrl",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cifras_cache");
        }
    }
}
