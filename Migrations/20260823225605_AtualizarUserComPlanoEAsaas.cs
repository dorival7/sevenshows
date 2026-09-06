using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SevenShows.Api.Migrations
{
    /// <inheritdoc />
    public partial class AtualizarUserComPlanoEAsaas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SaaSPlanId",
                table: "users",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "users",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"),
                columns: new[] { "SaaSPlanId", "SubscriptionStatus" },
                values: new object[] { null, "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_users_SaaSPlanId",
                table: "users",
                column: "SaaSPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_SaaSPlans_SaaSPlanId",
                table: "users",
                column: "SaaSPlanId",
                principalTable: "SaaSPlans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_SaaSPlans_SaaSPlanId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_SaaSPlanId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SaaSPlanId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "users");
        }
    }
}
