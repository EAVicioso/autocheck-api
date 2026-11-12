using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreserveSandboxAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                schema: "autocheck",
                table: "PaymentAttempt",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 1000m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "autocheck",
                table: "PaymentAttempt",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "DOP");
            migrationBuilder.Sql("""
                ALTER TABLE autocheck."PaymentAttempt" ADD CONSTRAINT positive_simulated_amount CHECK ("Amount" > 0 AND "Currency" = 'DOP');
                ALTER TABLE autocheck."Inspection" ADD CONSTRAINT consistent_verdict CHECK ("State" <> 'CERRADA' OR "Result" =
                  CASE WHEN "Serious" >= 1 OR "Medium" >= 2 OR "Minor" > "MaxMinor" THEN 'Rechazado' ELSE 'Aprobado' END);
                CREATE FUNCTION autocheck.protect_certificate_manifest() RETURNS trigger LANGUAGE plpgsql AS $manifest$
                BEGIN
                  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Reserved certificate identity is permanent' USING ERRCODE = '23514'; END IF;
                  IF NEW."Manifest" IS DISTINCT FROM OLD."Manifest" OR NEW."ManifestHash" IS DISTINCT FROM OLD."ManifestHash"
                     OR NEW."InspectionId" IS DISTINCT FROM OLD."InspectionId" OR NEW."OwnerId" IS DISTINCT FROM OLD."OwnerId" OR NEW."Issued" IS DISTINCT FROM OLD."Issued" THEN
                    RAISE EXCEPTION 'Reserved manifest is immutable' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END $manifest$;
                CREATE TRIGGER immutable_manifest BEFORE UPDATE OR DELETE ON autocheck."Certificate" FOR EACH ROW EXECUTE FUNCTION autocheck.protect_certificate_manifest();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Preserve simulated payment history with a reviewed forward migration.");
        }
    }
}
