using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracionReporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionesReporte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateID = table.Column<int>(type: "int", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColumnasVisibles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operaciones = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnaLineaPorForm = table.Column<bool>(type: "bit", nullable: false),
                    OcultarVacias = table.Column<bool>(type: "bit", nullable: false),
                    CompactarFilas = table.Column<bool>(type: "bit", nullable: false),
                    OmitirTotalesDelForm = table.Column<bool>(type: "bit", nullable: false),
                    SubtotalPorForm = table.Column<bool>(type: "bit", nullable: false),
                    ModoResumen = table.Column<bool>(type: "bit", nullable: false),
                    AgruparPor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActualizadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesReporte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostosProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Producto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Unidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActualizadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostosProducto", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesReporte_TemplateID",
                table: "ConfiguracionesReporte",
                column: "TemplateID",
                unique: true,
                filter: "[TemplateID] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesReporte");

            migrationBuilder.DropTable(
                name: "CostosProducto");
        }
    }
}
