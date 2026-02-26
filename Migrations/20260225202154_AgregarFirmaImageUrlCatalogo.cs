using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFirmaImageUrlCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirmaImageUrl",
                table: "CatalogoFirmas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirmaImageUrl",
                table: "CatalogoFirmas");
        }
    }
}
