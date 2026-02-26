using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTipoProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoProducto",
                table: "FilledForms",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoProducto",
                table: "FilledForms");
        }
    }
}
