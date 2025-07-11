using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DOAMapper.Migrations
{
    /// <inheritdoc />
    public partial class UpdateForeignKeyConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Alliances_AllianceId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Tiles_Alliances_AllianceId",
                table: "Tiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Tiles_Players_PlayerId",
                table: "Tiles");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Alliances_AllianceId",
                table: "Players",
                column: "AllianceId",
                principalTable: "Alliances",
                principalColumn: "AllianceId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tiles_Alliances_AllianceId",
                table: "Tiles",
                column: "AllianceId",
                principalTable: "Alliances",
                principalColumn: "AllianceId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tiles_Players_PlayerId",
                table: "Tiles",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Alliances_AllianceId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Tiles_Alliances_AllianceId",
                table: "Tiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Tiles_Players_PlayerId",
                table: "Tiles");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Alliances_AllianceId",
                table: "Players",
                column: "AllianceId",
                principalTable: "Alliances",
                principalColumn: "AllianceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tiles_Alliances_AllianceId",
                table: "Tiles",
                column: "AllianceId",
                principalTable: "Alliances",
                principalColumn: "AllianceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tiles_Players_PlayerId",
                table: "Tiles",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId");
        }
    }
}
