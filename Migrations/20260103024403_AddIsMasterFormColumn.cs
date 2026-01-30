using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormBuilder.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMasterFormColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ SOLO agregar IsMasterForm (FechaVersion ya existe)
            migrationBuilder.AddColumn<bool>(
                name: "IsMasterForm",
                table: "Templates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMasterForm",
                table: "Templates");
        }
    }
}
