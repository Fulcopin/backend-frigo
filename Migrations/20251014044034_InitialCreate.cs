using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    TemplateID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Objetivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Proceso = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CuandoSeUsa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuienLoLlena = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeaderFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TableColumns = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Firmas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.TemplateID);
                });

            migrationBuilder.CreateTable(
                name: "FilledForms",
                columns: table => new
                {
                    FormID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateID = table.Column<int>(type: "int", nullable: false),
                    HeaderData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirmasData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilledForms", x => x.FormID);
                    table.ForeignKey(
                        name: "FK_FilledForms_Templates_TemplateID",
                        column: x => x.TemplateID,
                        principalTable: "Templates",
                        principalColumn: "TemplateID",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_FilledForms_TemplateID",
                table: "FilledForms",
                column: "TemplateID");

            migrationBuilder.CreateIndex(
                name: "IX_TableRows_FormID",
                table: "TableRows",
                column: "FormID");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_Codigo",
                table: "Templates",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TableRows");

            migrationBuilder.DropTable(
                name: "FilledForms");

            migrationBuilder.DropTable(
                name: "Templates");
        }
    }
}
