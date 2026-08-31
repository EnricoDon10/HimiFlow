using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einsparungs.Api.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class OneRolePerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_AppUserId_Unique",
                table: "UserRoles",
                column: "AppUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_AppUserId_Unique",
                table: "UserRoles");
        }
    }
}
