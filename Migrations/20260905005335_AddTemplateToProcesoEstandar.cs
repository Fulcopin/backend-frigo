using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateToProcesoEstandar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormularioNombre",
                table: "ProcesoEstandares",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateID",
                table: "ProcesoEstandares",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcesoEstandares_TemplateID",
                table: "ProcesoEstandares",
                column: "TemplateID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcesoEstandares_Templates_TemplateID",
                table: "ProcesoEstandares",
                column: "TemplateID",
                principalTable: "Templates",
                principalColumn: "TemplateID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcesoEstandares_Templates_TemplateID",
                table: "ProcesoEstandares");

            migrationBuilder.DropIndex(
                name: "IX_ProcesoEstandares_TemplateID",
                table: "ProcesoEstandares");

            migrationBuilder.DropColumn(
                name: "FormularioNombre",
                table: "ProcesoEstandares");

            migrationBuilder.DropColumn(
                name: "TemplateID",
                table: "ProcesoEstandares");
        }
    }
}
