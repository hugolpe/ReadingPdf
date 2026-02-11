using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadingPdf.Migrations
{
    /// <inheritdoc />
    public partial class _2026012701 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BancoId",
                table: "Movimiento",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CuentaAplicada",
                table: "Movimiento",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaOriginal",
                table: "Movimiento",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcesoId",
                table: "Movimiento",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BancoId",
                table: "Movimiento");

            migrationBuilder.DropColumn(
                name: "CuentaAplicada",
                table: "Movimiento");

            migrationBuilder.DropColumn(
                name: "EmpresaOriginal",
                table: "Movimiento");

            migrationBuilder.DropColumn(
                name: "ProcesoId",
                table: "Movimiento");
        }
    }
}
