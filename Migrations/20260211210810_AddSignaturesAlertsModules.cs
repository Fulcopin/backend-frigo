using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSignaturesAlertsModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMasterForm",
                table: "Templates");

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Templates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Frecuencia",
                table: "Templates",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlertConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnableMissingFormAlerts = table.Column<bool>(type: "bit", nullable: false),
                    DailyCheckTime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissingFormRecipients = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnableSignatureAlerts = table.Column<bool>(type: "bit", nullable: false),
                    SignatureAlertDelay = table.Column<int>(type: "int", nullable: false),
                    SignatureRecipients = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormId = table.Column<int>(type: "int", nullable: true),
                    FormCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_FilledForms_FormId",
                        column: x => x.FormId,
                        principalTable: "FilledForms",
                        principalColumn: "FormID");
                });

            migrationBuilder.CreateTable(
                name: "Signatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FilledFormId = table.Column<int>(type: "int", nullable: false),
                    SignatureImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsModifiedBySGI = table.Column<bool>(type: "bit", nullable: false),
                    OriginalSignedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Signatures_FilledForms_FilledFormId",
                        column: x => x.FilledFormId,
                        principalTable: "FilledForms",
                        principalColumn: "FormID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_FormId",
                table: "Alerts",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_Signatures_FilledFormId",
                table: "Signatures",
                column: "FilledFormId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertConfigurations");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "Signatures");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "Frecuencia",
                table: "Templates");

            migrationBuilder.AddColumn<bool>(
                name: "IsMasterForm",
                table: "Templates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
