using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SevenShows.Api.Migrations
{
    /// <inheritdoc />
    public partial class AtualizarCamposRangeAgendaBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_artist_agenda_blocks_users_UserId",
                table: "artist_agenda_blocks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_artist_agenda_blocks",
                table: "artist_agenda_blocks");

            migrationBuilder.DropIndex(
                name: "IX_artist_agenda_blocks_UserId",
                table: "artist_agenda_blocks");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "artist_agenda_blocks");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "artist_agenda_blocks");

            migrationBuilder.RenameTable(
                name: "artist_agenda_blocks",
                newName: "artistagendablocks");

            migrationBuilder.RenameColumn(
                name: "BlockDate",
                table: "artistagendablocks",
                newName: "StartDate");

            migrationBuilder.UpdateData(
                table: "artistagendablocks",
                keyColumn: "Reason",
                keyValue: null,
                column: "Reason",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "artistagendablocks",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "artistagendablocks",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_artistagendablocks",
                table: "artistagendablocks",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_artistagendablocks",
                table: "artistagendablocks");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "artistagendablocks");

            migrationBuilder.RenameTable(
                name: "artistagendablocks",
                newName: "artist_agenda_blocks");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "artist_agenda_blocks",
                newName: "BlockDate");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "artist_agenda_blocks",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndTime",
                table: "artist_agenda_blocks",
                type: "time(6)",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartTime",
                table: "artist_agenda_blocks",
                type: "time(6)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_artist_agenda_blocks",
                table: "artist_agenda_blocks",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_artist_agenda_blocks_UserId",
                table: "artist_agenda_blocks",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_artist_agenda_blocks_users_UserId",
                table: "artist_agenda_blocks",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
