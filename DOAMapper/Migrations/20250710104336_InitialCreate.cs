using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DOAMapper.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RecordsProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordsChanged = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Alliances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllianceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ImportSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OverlordName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Power = table.Column<long>(type: "INTEGER", nullable: false),
                    FortressLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    FortressX = table.Column<int>(type: "INTEGER", nullable: false),
                    FortressY = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alliances", x => x.Id);
                    table.UniqueConstraint("AK_Alliances_AllianceId", x => x.AllianceId);
                    table.ForeignKey(
                        name: "FK_Alliances_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ImportSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Might = table.Column<long>(type: "INTEGER", nullable: false),
                    AllianceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.UniqueConstraint("AK_Players_PlayerId", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_Players_Alliances_AllianceId",
                        column: x => x.AllianceId,
                        principalTable: "Alliances",
                        principalColumn: "AllianceId");
                    table.ForeignKey(
                        name: "FK_Players_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    X = table.Column<int>(type: "INTEGER", nullable: false),
                    Y = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AllianceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tiles_Alliances_AllianceId",
                        column: x => x.AllianceId,
                        principalTable: "Alliances",
                        principalColumn: "AllianceId");
                    table.ForeignKey(
                        name: "FK_Tiles_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tiles_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alliances_AllianceId",
                table: "Alliances",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_Alliances_AllianceId_ValidFrom",
                table: "Alliances",
                columns: new[] { "AllianceId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Alliances_ImportSessionId",
                table: "Alliances",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Alliances_IsActive_ValidFrom",
                table: "Alliances",
                columns: new[] { "IsActive", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Alliances_Name",
                table: "Alliances",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_ImportDate",
                table: "ImportSessions",
                column: "ImportDate");

            migrationBuilder.CreateIndex(
                name: "IX_Players_AllianceId",
                table: "Players",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_ImportSessionId",
                table: "Players",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_IsActive_ValidFrom",
                table: "Players",
                columns: new[] { "IsActive", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name",
                table: "Players",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerId",
                table: "Players",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerId_ValidFrom",
                table: "Players",
                columns: new[] { "PlayerId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Tiles_AllianceId",
                table: "Tiles",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tiles_ImportSessionId",
                table: "Tiles",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tiles_IsActive_ValidFrom",
                table: "Tiles",
                columns: new[] { "IsActive", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Tiles_PlayerId",
                table: "Tiles",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tiles_PlayerId_ValidFrom",
                table: "Tiles",
                columns: new[] { "PlayerId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Tiles_X_Y",
                table: "Tiles",
                columns: new[] { "X", "Y" });

            migrationBuilder.CreateIndex(
                name: "IX_Tiles_X_Y_ValidFrom",
                table: "Tiles",
                columns: new[] { "X", "Y", "ValidFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tiles");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Alliances");

            migrationBuilder.DropTable(
                name: "ImportSessions");
        }
    }
}
