using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SevenShows.Api.Migrations
{
    /// <inheritdoc />
    public partial class CriarEstruturaDeSegurancaESeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "AsaasWalletId", "CreatedAt", "DocumentNumber", "Email", "KycStatus", "Name", "PasswordHash", "PersonType" },
                values: new object[] { new Guid("77777777-7777-7777-7777-777777777777"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "00.000.000/0001-00", "admin@sevenshows.com.br", "Approved", "Administrador SevenShows", "$2a$11$R9h/bV7D7X7V7G7K7B7.eO7Xp7V7X7G7K7B7.eO7Xp7V7X7G7K7B.", "Legal" });

            migrationBuilder.InsertData(
                table: "user_roles",
                columns: new[] { "role_id", "user_id" },
                values: new object[] { "SuperAdmin", new Guid("77777777-7777-7777-7777-777777777777") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "user_roles",
                keyColumns: new[] { "role_id", "user_id" },
                keyValues: new object[] { "SuperAdmin", new Guid("77777777-7777-7777-7777-777777777777") });

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"));
        }
    }
}
