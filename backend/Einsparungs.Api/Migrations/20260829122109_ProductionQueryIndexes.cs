using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einsparungs.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProductionQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_CreatedByUserId",
                table: "SavingsEntries");

            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_ProductGroupId",
                table: "SavingsEntries");

            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_SavingReasonId",
                table: "SavingsEntries");

            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_TeamId",
                table: "SavingsEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_ActiveMonthCreatedAt",
                table: "SavingsEntries",
                columns: new[] { "IsDeleted", "Month", "CreatedAt" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_ProductGroupActiveMonth",
                table: "SavingsEntries",
                columns: new[] { "ProductGroupId", "IsDeleted", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_ReasonActiveMonth",
                table: "SavingsEntries",
                columns: new[] { "SavingReasonId", "IsDeleted", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_TeamActiveMonth",
                table: "SavingsEntries",
                columns: new[] { "TeamId", "IsDeleted", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_UserActiveMonthCreatedAt",
                table: "SavingsEntries",
                columns: new[] { "CreatedByUserId", "IsDeleted", "Month", "CreatedAt" },
                descending: new[] { false, false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_ActiveMonthCreatedAt",
                table: "SavingsEntries");

            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_ProductGroupActiveMonth",
                table: "SavingsEntries");

            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_ReasonActiveMonth",
                table: "SavingsEntries");

            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_TeamActiveMonth",
                table: "SavingsEntries");

            migrationBuilder.DropIndex(
                name: "IX_SavingsEntries_UserActiveMonthCreatedAt",
                table: "SavingsEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_CreatedByUserId",
                table: "SavingsEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_ProductGroupId",
                table: "SavingsEntries",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_SavingReasonId",
                table: "SavingsEntries",
                column: "SavingReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsEntries_TeamId",
                table: "SavingsEntries",
                column: "TeamId");
        }
    }
}
