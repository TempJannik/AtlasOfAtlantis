using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DOAMapper.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressPercentageToImportSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressPercentage",
                table: "ImportSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgressPercentage",
                table: "ImportSessions");
        }
    }
}
