using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SevenShows.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAsaasPaymentIdToArtistEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AsaasPaymentId",
                table: "artistevents",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "BirthDate",
                value: new DateTime(1979, 12, 31, 22, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsaasPaymentId",
                table: "artistevents");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                column: "BirthDate",
                value: new DateTime(1980, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
        }
    }
}
