using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DOAMapper.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceImportSessionProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentPhase",
                table: "ImportSessions",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CurrentPhaseNumber",
                table: "ImportSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentPhaseProgressPercentage",
                table: "ImportSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastProgressUpdate",
                table: "ImportSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PhaseDetailsJson",
                table: "ImportSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusMessage",
                table: "ImportSessions",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TotalPhases",
                table: "ImportSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_CreatedAt",
                table: "ImportSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_Status",
                table: "ImportSessions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportSessions_CreatedAt",
                table: "ImportSessions");

            migrationBuilder.DropIndex(
                name: "IX_ImportSessions_Status",
                table: "ImportSessions");

            migrationBuilder.DropColumn(
                name: "CurrentPhase",
                table: "ImportSessions");

            migrationBuilder.DropColumn(
                name: "CurrentPhaseNumber",
                table: "ImportSessions");

            migrationBuilder.DropColumn(
                name: "CurrentPhaseProgressPercentage",
                table: "ImportSessions");

            migrationBuilder.DropColumn(
                name: "LastProgressUpdate",
                table: "ImportSessions");

            migrationBuilder.DropColumn(
                name: "PhaseDetailsJson",
                table: "ImportSessions");

            migrationBuilder.DropColumn(
                name: "StatusMessage",
                table: "ImportSessions");

            migrationBuilder.DropColumn(
                name: "TotalPhases",
                table: "ImportSessions");
        }
    }
}
