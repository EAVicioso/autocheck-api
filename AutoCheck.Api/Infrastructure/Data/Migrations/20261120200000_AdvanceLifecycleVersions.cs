using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AutoCheck.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AutoCheckDb))]
[Migration("20261120200000_AdvanceLifecycleVersions")]
public sealed class AdvanceLifecycleVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION autocheck.advance_lifecycle_version()
        RETURNS trigger LANGUAGE plpgsql SET search_path = pg_catalog AS $version$
        BEGIN
            IF NEW."Version" IS NOT DISTINCT FROM OLD."Version" THEN
                NEW."Version" := OLD."Version" + 1;
            END IF;
            RETURN NEW;
        END;
        $version$;

        DROP TRIGGER IF EXISTS lifecycle_version ON autocheck."UserAccount";
        CREATE TRIGGER lifecycle_version BEFORE UPDATE ON autocheck."UserAccount"
        FOR EACH ROW EXECUTE FUNCTION autocheck.advance_lifecycle_version();

        DROP TRIGGER IF EXISTS lifecycle_version ON autocheck."Vehicle";
        CREATE TRIGGER lifecycle_version BEFORE UPDATE ON autocheck."Vehicle"
        FOR EACH ROW EXECUTE FUNCTION autocheck.advance_lifecycle_version();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Lifecycle version protection requires a forward migration.");
}
