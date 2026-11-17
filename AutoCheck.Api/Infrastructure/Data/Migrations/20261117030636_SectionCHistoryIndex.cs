using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SectionCHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Inspection_VehicleId_Started_Closed",
                schema: "autocheck",
                table: "Inspection",
                columns: new[] { "VehicleId", "Started", "Closed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inspection_VehicleId_Started_Closed",
                schema: "autocheck",
                table: "Inspection");
        }
    }
}
