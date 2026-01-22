using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadingPdf.Migrations
{
    /// <inheritdoc />
    public partial class _2025111201 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DateFormat",
                table: "Bank",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "TTerceros",
                columns: table => new
                {
                    IdTercero = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoTerceroId = table.Column<int>(type: "int", nullable: false),
                    IdTipTercero = table.Column<int>(type: "int", nullable: false),
                    Nid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdTipDocumento = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NombreComercial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdDirection = table.Column<int>(type: "int", nullable: false),
                    TelefonoId = table.Column<int>(type: "int", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Credit = table.Column<bool>(type: "bit", nullable: false),
                    Image = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    TercCodContId = table.Column<int>(type: "int", nullable: false),
                    PorDefecto = table.Column<bool>(type: "bit", nullable: false),
                    TercModVenta = table.Column<bool>(type: "bit", nullable: false),
                    TercModCompras = table.Column<bool>(type: "bit", nullable: false),
                    TercModHis = table.Column<bool>(type: "bit", nullable: false),
                    TercModNomina = table.Column<bool>(type: "bit", nullable: false),
                    TerConPoliza = table.Column<bool>(type: "bit", nullable: false),
                    TerConVehiculo = table.Column<bool>(type: "bit", nullable: false),
                    TerPolComId = table.Column<int>(type: "int", nullable: false),
                    TerPoliza = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TerPolFecVen = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TTerceros", x => x.IdTercero);
                });

            migrationBuilder.CreateTable(
                name: "TDireccionT",
                columns: table => new
                {
                    IdDirection = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DirDescrip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CodCiudad = table.Column<int>(type: "int", nullable: false),
                    TTercerosIdTercero = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TDireccionT", x => x.IdDirection);
                    table.ForeignKey(
                        name: "FK_TDireccionT_TTerceros_TTercerosIdTercero",
                        column: x => x.TTercerosIdTercero,
                        principalTable: "TTerceros",
                        principalColumn: "IdTercero",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TDireccionT_TTercerosIdTercero",
                table: "TDireccionT",
                column: "TTercerosIdTercero");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TDireccionT");

            migrationBuilder.DropTable(
                name: "TTerceros");

            migrationBuilder.AlterColumn<string>(
                name: "DateFormat",
                table: "Bank",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
