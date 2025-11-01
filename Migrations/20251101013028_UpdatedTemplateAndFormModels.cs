using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedTemplateAndFormModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TableRows");

            migrationBuilder.RenameColumn(
                name: "TableColumns",
                table: "Templates",
                newName: "BodyElements");

            migrationBuilder.AddColumn<string>(
                name: "BodyData",
                table: "FilledForms",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyData",
                table: "FilledForms");

            migrationBuilder.RenameColumn(
                name: "BodyElements",
                table: "Templates",
                newName: "TableColumns");

            migrationBuilder.CreateTable(
                name: "TableRows",
                columns: table => new
                {
                    RowID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormID = table.Column<int>(type: "int", nullable: false),
                    RowData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableRows", x => x.RowID);
                    table.ForeignKey(
                        name: "FK_TableRows_FilledForms_FormID",
                        column: x => x.FormID,
                        principalTable: "FilledForms",
                        principalColumn: "FormID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TableRows_FormID",
                table: "TableRows",
                column: "FormID");
        }
    }
}
