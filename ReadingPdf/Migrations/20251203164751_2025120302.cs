using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadingPdf.Migrations
{
    /// <inheritdoc />
    public partial class _2025120302 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EmpresaId",
                table: "empresas",
                newName: "Id");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "empresas",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "empresas");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "empresas",
                newName: "EmpresaId");
        }
    }
}
