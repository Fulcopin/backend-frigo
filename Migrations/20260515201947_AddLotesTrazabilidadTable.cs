using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLotesTrazabilidadTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotesTrazabilidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormID = table.Column<int>(type: "int", nullable: false),
                    LoteOrigen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoteDestino = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Proceso = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Producto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CantidadEntrada = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CantidadSalida = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesTrazabilidad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotesTrazabilidad_FilledForms_FormID",
                        column: x => x.FormID,
                        principalTable: "FilledForms",
                        principalColumn: "FormID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotesTrazabilidad_FormID",
                table: "LotesTrazabilidad",
                column: "FormID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotesTrazabilidad");
        }
    }
}
