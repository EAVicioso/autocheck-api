using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenDatabaseBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BookingBlock_State_Expires",
                schema: "autocheck",
                table: "BookingBlock",
                columns: new[] { "State", "Expires" });
            migrationBuilder.Sql("""
                ALTER FUNCTION autocheck.protect_history() SET search_path = '';
                ALTER FUNCTION autocheck.protect_certificate_manifest() SET search_path = '';
                REVOKE ALL ON ALL FUNCTIONS IN SCHEMA autocheck FROM PUBLIC;
                REVOKE ALL ON public."__EFMigrationsHistory" FROM PUBLIC;
                ALTER TABLE public."__EFMigrationsHistory" ENABLE ROW LEVEL SECURITY;
                DO $boundary$ BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                    REVOKE ALL ON ALL TABLES IN SCHEMA autocheck FROM anon;
                    REVOKE ALL ON ALL FUNCTIONS IN SCHEMA autocheck FROM anon;
                    REVOKE ALL ON public."__EFMigrationsHistory" FROM anon;
                  END IF;
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                    REVOKE ALL ON ALL TABLES IN SCHEMA autocheck FROM authenticated;
                    REVOKE ALL ON ALL FUNCTIONS IN SCHEMA autocheck FROM authenticated;
                    REVOKE ALL ON public."__EFMigrationsHistory" FROM authenticated;
                  END IF;
                END $boundary$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Database access protections require a reviewed forward migration.");
        }
    }
}
