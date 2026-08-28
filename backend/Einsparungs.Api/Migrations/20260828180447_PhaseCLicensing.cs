using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einsparungs.Api.Migrations
{
    /// <inheritdoc />
    public partial class PhaseCLicensing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LicenseInstallations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LicenseKey = table.Column<string>(type: "TEXT", maxLength: 12000, nullable: false),
                    InstalledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InstalledByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseInstallations_Users_InstalledByUserId",
                        column: x => x.InstalledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseInstallations_InstalledByUserId",
                table: "LicenseInstallations",
                column: "InstalledByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenseInstallations");
        }
    }
}
