using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SevenShows.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCamposLogisticaAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ArtistEvents",
                table: "ArtistEvents");

            migrationBuilder.RenameTable(
                name: "ArtistEvents",
                newName: "artistevents");

            migrationBuilder.AddColumn<Guid>(
                name: "ArtistPackageId",
                table: "artistevents",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePackagePrice",
                table: "artistevents",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ContractorName",
                table: "artistevents",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "artistevents",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DistanceKm",
                table: "artistevents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "artistevents",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ExtraHours",
                table: "artistevents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraHoursValueCharged",
                table: "artistevents",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ExtraKm",
                table: "artistevents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraKmValueCharged",
                table: "artistevents",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RequestedDurationHours",
                table: "artistevents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalProposedPrice",
                table: "artistevents",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_artistevents",
                table: "artistevents",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_artistevents_ArtistPackageId",
                table: "artistevents",
                column: "ArtistPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_artistevents_UserId",
                table: "artistevents",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_artistevents_artist_packages_ArtistPackageId",
                table: "artistevents",
                column: "ArtistPackageId",
                principalTable: "artist_packages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_artistevents_users_UserId",
                table: "artistevents",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_artistevents_artist_packages_ArtistPackageId",
                table: "artistevents");

            migrationBuilder.DropForeignKey(
                name: "FK_artistevents_users_UserId",
                table: "artistevents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_artistevents",
                table: "artistevents");

            migrationBuilder.DropIndex(
                name: "IX_artistevents_ArtistPackageId",
                table: "artistevents");

            migrationBuilder.DropIndex(
                name: "IX_artistevents_UserId",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "ArtistPackageId",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "BasePackagePrice",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "ContractorName",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "DistanceKm",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "ExtraHours",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "ExtraHoursValueCharged",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "ExtraKm",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "ExtraKmValueCharged",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "RequestedDurationHours",
                table: "artistevents");

            migrationBuilder.DropColumn(
                name: "TotalProposedPrice",
                table: "artistevents");

            migrationBuilder.RenameTable(
                name: "artistevents",
                newName: "ArtistEvents");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ArtistEvents",
                table: "ArtistEvents",
                column: "Id");
        }
    }
}
