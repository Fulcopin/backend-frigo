using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTA: la tabla ConsultasPlan ya existe en la base de datos (fue creada
            // manualmente vía Migrations/CreateConsultasPlanTable.sql, fuera del
            // historial de EF Migrations). Se omite aquí para no intentar recrearla.

            migrationBuilder.CreateTable(
                name: "PersonalRegistros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormID = table.Column<int>(type: "int", nullable: false),
                    TemplateId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Proceso = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    PersonalPlanta = table.Column<int>(type: "int", nullable: false),
                    PersonalExterno = table.Column<int>(type: "int", nullable: false),
                    HoraDesde = table.Column<TimeSpan>(type: "time", nullable: false),
                    HoraHasta = table.Column<TimeSpan>(type: "time", nullable: false),
                    HorasTrabajadas = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CumpleEstandar = table.Column<bool>(type: "bit", nullable: false),
                    MotivoIncumplimiento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    JustificacionVariacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalRegistros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalRegistros_FilledForms_FormID",
                        column: x => x.FormID,
                        principalTable: "FilledForms",
                        principalColumn: "FormID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcesoEstandares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Proceso = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HoraInicioEsperada = table.Column<TimeSpan>(type: "time", nullable: true),
                    HoraFinEsperada = table.Column<TimeSpan>(type: "time", nullable: true),
                    DuracionMinimaHoras = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    DuracionMaximaHoras = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    ToleranciaMinutos = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcesoEstandares", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRegistros_FormID",
                table: "PersonalRegistros",
                column: "FormID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonalRegistros");

            migrationBuilder.DropTable(
                name: "ProcesoEstandares");
        }
    }
}
