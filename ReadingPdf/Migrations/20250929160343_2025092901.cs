using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadingPdf.Migrations
{
    /// <inheritdoc />
    public partial class _2025092901 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateFormat",
                table: "Bank",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateFormat",
                table: "Bank");
        }
    }
}
