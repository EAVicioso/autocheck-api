-- Run as the Supabase database administrator AFTER the reviewed EF migrations.
-- No passwords belong here. Provision login credentials separately in protected configuration.
-- Idempotent; does not remove data or change the legacy public schema.
DO $roles$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'autocheck_api') THEN
    CREATE ROLE autocheck_api LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS CONNECTION LIMIT 15;
  END IF;
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'autocheck_migrations') THEN
    CREATE ROLE autocheck_migrations LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS CONNECTION LIMIT 3;
  END IF;
END $roles$;
ALTER ROLE autocheck_api CONNECTION LIMIT 15;
GRANT autocheck_migrations TO postgres;
GRANT CONNECT ON DATABASE postgres TO autocheck_api, autocheck_migrations;
-- Npgsql always issues CREATE TABLE IF NOT EXISTS for its ledger, even when present.
-- Only the trusted migration login receives CREATE in public; the runtime never does.
GRANT USAGE, CREATE ON SCHEMA public TO autocheck_migrations;
ALTER SCHEMA autocheck OWNER TO autocheck_migrations;
DO $ownership$ DECLARE item record; BEGIN
  FOR item IN SELECT tablename FROM pg_tables WHERE schemaname = 'autocheck' LOOP
    EXECUTE format('ALTER TABLE autocheck.%I OWNER TO autocheck_migrations', item.tablename);
  END LOOP;
  FOR item IN SELECT p.oid::regprocedure AS signature FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'autocheck' LOOP
    EXECUTE format('ALTER FUNCTION %s OWNER TO autocheck_migrations', item.signature);
  END LOOP;
END $ownership$;
-- The administrator retains ownership of the ledger in public.
GRANT SELECT, INSERT, UPDATE, DELETE ON public."__EFMigrationsHistory" TO autocheck_migrations;
DROP POLICY IF EXISTS backend_migration_ledger ON public."__EFMigrationsHistory";
CREATE POLICY backend_migration_ledger ON public."__EFMigrationsHistory" FOR ALL TO autocheck_migrations USING (true) WITH CHECK (true);
REVOKE ALL ON SCHEMA autocheck FROM PUBLIC, anon, authenticated;
REVOKE ALL ON ALL TABLES IN SCHEMA autocheck FROM PUBLIC, anon, authenticated;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA autocheck FROM PUBLIC, anon, authenticated;
GRANT USAGE ON SCHEMA autocheck TO autocheck_api;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA autocheck TO autocheck_api;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA autocheck TO autocheck_api;
REVOKE UPDATE, DELETE ON autocheck."AuditEvent", autocheck."InspectionRule" FROM autocheck_api;
REVOKE DELETE ON autocheck."Certificate" FROM autocheck_api;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA autocheck TO autocheck_api;
REVOKE ALL ON public."__EFMigrationsHistory" FROM PUBLIC, anon, authenticated, autocheck_api;
GRANT SELECT ON public."__EFMigrationsHistory" TO autocheck_api;
DROP POLICY IF EXISTS backend_migration_status ON public."__EFMigrationsHistory";
CREATE POLICY backend_migration_status ON public."__EFMigrationsHistory" FOR SELECT TO autocheck_api USING (true);
ALTER DEFAULT PRIVILEGES FOR ROLE autocheck_migrations IN SCHEMA autocheck REVOKE ALL ON TABLES FROM PUBLIC, anon, authenticated;
ALTER DEFAULT PRIVILEGES FOR ROLE autocheck_migrations IN SCHEMA autocheck GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO autocheck_api;
ALTER DEFAULT PRIVILEGES FOR ROLE autocheck_migrations IN SCHEMA autocheck GRANT USAGE, SELECT ON SEQUENCES TO autocheck_api;
ALTER DEFAULT PRIVILEGES FOR ROLE autocheck_migrations IN SCHEMA autocheck REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER DEFAULT PRIVILEGES FOR ROLE autocheck_migrations REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
ALTER DEFAULT PRIVILEGES FOR ROLE autocheck_migrations IN SCHEMA autocheck GRANT EXECUTE ON FUNCTIONS TO autocheck_api;
ALTER ROLE autocheck_api SET statement_timeout = '30s';
ALTER ROLE autocheck_api SET idle_in_transaction_session_timeout = '30s';
ALTER ROLE autocheck_migrations SET statement_timeout = '120s';
ALTER ROLE autocheck_migrations SET idle_in_transaction_session_timeout = '120s';
