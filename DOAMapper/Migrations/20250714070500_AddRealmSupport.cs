using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DOAMapper.Migrations
{
    /// <inheritdoc />
    public partial class AddRealmSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RealmId",
                table: "ImportSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Realms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RealmId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Realms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_RealmId",
                table: "ImportSessions",
                column: "RealmId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_RealmId_ImportDate",
                table: "ImportSessions",
                columns: new[] { "RealmId", "ImportDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Realms_IsActive",
                table: "Realms",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Realms_Name",
                table: "Realms",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Realms_RealmId",
                table: "Realms",
                column: "RealmId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportSessions_Realms_RealmId",
                table: "ImportSessions",
                column: "RealmId",
                principalTable: "Realms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportSessions_Realms_RealmId",
                table: "ImportSessions");

            migrationBuilder.DropTable(
                name: "Realms");

            migrationBuilder.DropIndex(
                name: "IX_ImportSessions_RealmId",
                table: "ImportSessions");

            migrationBuilder.DropIndex(
                name: "IX_ImportSessions_RealmId_ImportDate",
                table: "ImportSessions");

            migrationBuilder.DropColumn(
                name: "RealmId",
                table: "ImportSessions");
        }
    }
}
