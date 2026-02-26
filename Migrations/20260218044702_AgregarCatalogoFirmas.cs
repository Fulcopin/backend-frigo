using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCatalogoFirmas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogoFirmas",
                columns: table => new
                {
                    CatalogoFirmaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Puesto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogoFirmas", x => x.CatalogoFirmaID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogoFirmas");
        }
    }
}
