using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SevenShows.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurarLimiteDesignerAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `SaaSPlans` SET `MaxDesignerAssets` = 10 WHERE `MaxDesignerAssets` = 0;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `SaaSPlans` SET `MaxDesignerAssets` = 0 WHERE `MaxDesignerAssets` = 10;"
            );
        }
    }
}