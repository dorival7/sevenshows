using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SevenShows.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContractorIdToArtistEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContractorId",
                table: "artistevents",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractorId",
                table: "artistevents");
        }
    }
}
