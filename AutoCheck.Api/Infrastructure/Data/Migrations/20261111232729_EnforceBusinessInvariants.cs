using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCheck.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceBusinessInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                REVOKE ALL ON SCHEMA autocheck FROM PUBLIC;
                DO $grants$ BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                    REVOKE ALL ON SCHEMA autocheck FROM anon;
                    REVOKE ALL ON ALL TABLES IN SCHEMA autocheck FROM anon;
                  END IF;
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                    REVOKE ALL ON SCHEMA autocheck FROM authenticated;
                    REVOKE ALL ON ALL TABLES IN SCHEMA autocheck FROM authenticated;
                  END IF;
                END $grants$;

                ALTER TABLE autocheck."UserAccount" ADD CONSTRAINT valid_role CHECK ("Role" IN ('Ciudadano','Inspector','Administrador'));
                ALTER TABLE autocheck."InspectionRule" ADD CONSTRAINT valid_m CHECK ("MaxMinor" >= 0);
                ALTER TABLE autocheck."Inspection" ADD CONSTRAINT valid_counts CHECK ("Minor" >= 0 AND "Medium" >= 0 AND "Serious" >= 0 AND "MaxMinor" >= 0);
                ALTER TABLE autocheck."Inspection" ADD CONSTRAINT complete_closed CHECK ("State" <> 'CERRADA' OR ("Closed" IS NOT NULL AND "Defects" IS NOT NULL AND "Result" IN ('Aprobado','Rechazado')));
                ALTER TABLE autocheck."Certificate" ADD CONSTRAINT complete_certificate CHECK ("State" <> 'EMITIDO' OR ("Pdf" IS NOT NULL AND length("PdfHash") = 64 AND length("ManifestHash") = 64));

                CREATE FUNCTION autocheck.protect_history() RETURNS trigger LANGUAGE plpgsql AS $history$
                BEGIN
                  IF TG_TABLE_NAME IN ('AuditEvent','InspectionRule') THEN
                    RAISE EXCEPTION 'Append-only record' USING ERRCODE = '23514';
                  END IF;
                  IF TG_TABLE_NAME = 'Inspection' THEN
                    IF OLD."State" = 'CERRADA' THEN RAISE EXCEPTION 'Closed inspection is immutable' USING ERRCODE = '23514'; END IF;
                    IF TG_OP = 'UPDATE' AND (NEW."RuleVersion" IS DISTINCT FROM OLD."RuleVersion" OR NEW."MaxMinor" IS DISTINCT FROM OLD."MaxMinor") THEN
                      RAISE EXCEPTION 'Inspection rules are frozen at start' USING ERRCODE = '23514';
                    END IF;
                  END IF;
                  IF TG_TABLE_NAME = 'Certificate' AND OLD."State" = 'EMITIDO' THEN RAISE EXCEPTION 'Published certificate is immutable' USING ERRCODE = '23514'; END IF;
                  IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                END $history$;
                CREATE TRIGGER immutable_audit BEFORE UPDATE OR DELETE ON autocheck."AuditEvent" FOR EACH ROW EXECUTE FUNCTION autocheck.protect_history();
                CREATE TRIGGER immutable_rules BEFORE UPDATE OR DELETE ON autocheck."InspectionRule" FOR EACH ROW EXECUTE FUNCTION autocheck.protect_history();
                CREATE TRIGGER immutable_inspection BEFORE UPDATE OR DELETE ON autocheck."Inspection" FOR EACH ROW EXECUTE FUNCTION autocheck.protect_history();
                CREATE TRIGGER immutable_certificate BEFORE UPDATE OR DELETE ON autocheck."Certificate" FOR EACH ROW EXECUTE FUNCTION autocheck.protect_history();

                -- Optional bridge for the documented legacy schema. Original tables are preserved.
                DO $legacy$ BEGIN
                  IF to_regclass('public.usuario') IS NOT NULL AND to_regclass('public.rol') IS NOT NULL AND to_regclass('auth.users') IS NOT NULL AND to_regclass('public.centro') IS NOT NULL THEN
                    EXECUTE 'INSERT INTO autocheck."Center" ("Id","Name","Address","Active") SELECT cen_id, coalesce(cen_nombre,''Centro''), coalesce(cen_calle,''''), true FROM public.centro ON CONFLICT ("Id") DO NOTHING';
                    EXECUTE $import$
                      INSERT INTO autocheck."UserAccount" ("Id","AuthId","Email","Name","Phone","Role","Permissions","CenterId","Active")
                      SELECT u.usu_id, u.usu_auth_sup, lower(trim(u.usu_email)), coalesce(u.usu_nombre,''), coalesce(u.usu_celular,''),
                        CASE r.rol_nombre WHEN 'Propietario' THEN 'Ciudadano' WHEN 'Inspector' THEN 'Inspector' ELSE 'Administrador' END,
                        CASE r.rol_nombre WHEN 'AdminINTRANT' THEN ARRAY['usuarios.gestionar','centros.gestionar','reglas.gestionar','auditoria.consultar']
                          WHEN 'AdminTaller' THEN ARRAY['usuarios.gestionar','centros.gestionar'] ELSE ARRAY[]::text[] END,
                        CASE WHEN r.rol_nombre = 'AdminINTRANT' THEN NULL ELSE c.cen_id END,
                        u.usu_estado AND (r.rol_nombre IN ('Propietario','AdminINTRANT') OR c.cen_id IS NOT NULL)
                      FROM public.usuario u JOIN public.rol r ON r.rol_id = u.usu_rol JOIN auth.users a ON a.id = u.usu_auth_sup
                      LEFT JOIN public.centro c ON c.cen_id::text = a.raw_app_meta_data->>'cen_id'
                      WHERE u.usu_auth_sup IS NOT NULL AND u.usu_email IS NOT NULL AND r.rol_nombre IN ('Propietario','Inspector','AdminTaller','AdminINTRANT')
                      ON CONFLICT ("AuthId") DO NOTHING
                    $import$;
                  END IF;
                END $legacy$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("History-protection migrations are forward-only. Restore a reviewed backup instead of discarding academic evidence.");
        }
    }
}
