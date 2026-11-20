using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SectionCQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Inspection_InspectorId_CenterId_Started_Id",
                schema: "autocheck",
                table: "Inspection",
                columns: new[] { "InspectorId", "CenterId", "Started", "Id" },
                descending: new[] { false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_OwnerId_Started_Id",
                schema: "autocheck",
                table: "Inspection",
                columns: new[] { "OwnerId", "Started", "Id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_Started_Id",
                schema: "autocheck",
                table: "Inspection",
                columns: new[] { "Started", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyAlert_Day_Id",
                schema: "autocheck",
                table: "AnomalyAlert",
                columns: new[] { "Day", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inspection_InspectorId_CenterId_Started_Id",
                schema: "autocheck",
                table: "Inspection");

            migrationBuilder.DropIndex(
                name: "IX_Inspection_OwnerId_Started_Id",
                schema: "autocheck",
                table: "Inspection");

            migrationBuilder.DropIndex(
                name: "IX_Inspection_Started_Id",
                schema: "autocheck",
                table: "Inspection");

            migrationBuilder.DropIndex(
                name: "IX_AnomalyAlert_Day_Id",
                schema: "autocheck",
                table: "AnomalyAlert");
        }
    }
}
