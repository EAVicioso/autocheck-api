// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackGeminiQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeminiQuotaReservation",
                schema: "autocheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EstimatedInputTokens = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeminiQuotaReservation", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeminiQuotaReservation_ReservedAt",
                schema: "autocheck",
                table: "GeminiQuotaReservation",
                column: "ReservedAt");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autocheck_api') THEN
                        GRANT SELECT, INSERT, DELETE ON autocheck."GeminiQuotaReservation" TO autocheck_api;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeminiQuotaReservation",
                schema: "autocheck");
        }
    }
}
