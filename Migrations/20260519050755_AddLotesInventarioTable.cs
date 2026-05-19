using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLotesInventarioTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotesInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroLote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Proceso = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Producto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Clasificacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PesoEntrada = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Desperdicio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TipoDesperdicio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PesoNeto = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LotePadre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FormId = table.Column<int>(type: "int", nullable: true),
                    TemplateId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Fecha = table.Column<DateTime>(type: "date", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesInventario", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotesInventario");
        }
    }
}
