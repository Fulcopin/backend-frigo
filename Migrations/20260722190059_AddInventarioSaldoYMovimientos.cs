using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioSaldoYMovimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Saldo",
                table: "LotesInventario",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            // Backfill: los lotes YA existentes conservan su saldo. Los consumidos quedan en 0;
            // el resto arranca con su Peso Neto disponible.
            migrationBuilder.Sql(
                "UPDATE LotesInventario SET Saldo = CASE WHEN Estado = 'consumido' THEN 0 ELSE PesoNeto END;");

            migrationBuilder.CreateTable(
                name: "MovimientosInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteInventarioId = table.Column<int>(type: "int", nullable: false),
                    NumeroLote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SaldoResultante = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Proceso = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FormId = table.Column<int>(type: "int", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosInventario", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosInventario");

            migrationBuilder.DropColumn(
                name: "Saldo",
                table: "LotesInventario");
        }
    }
}
