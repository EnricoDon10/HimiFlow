using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einsparungs.Api.Migrations
{
    /// <inheritdoc />
    public partial class LicenseEnforcementHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulLicenseValidationUtc",
                table: "LicenseInstallations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSuccessfulLicenseValidationUtc",
                table: "LicenseInstallations");
        }
    }
}
