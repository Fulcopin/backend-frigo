using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class ReemplazarObjetivoConSupervisa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Objetivo",
                table: "TemplateVersions",
                newName: "Supervisa");

            migrationBuilder.RenameColumn(
                name: "Objetivo",
                table: "Templates",
                newName: "Supervisa");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Supervisa",
                table: "TemplateVersions",
                newName: "Objetivo");

            migrationBuilder.RenameColumn(
                name: "Supervisa",
                table: "Templates",
                newName: "Objetivo");
        }
    }
}
