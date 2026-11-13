using AutoCheck.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class PasswordResetSchemaTests : DatabaseTest
{
    private readonly Guid legacyUserId = Guid.NewGuid();
    private readonly Guid legacySessionId = Guid.NewGuid();

    protected override async Task BeforeMigrations(AutoCheckDb db)
    {
        await db.GetService<IMigrator>().MigrateAsync("20261112050410_HardenDatabaseBoundary");
        await db.Database.ExecuteSqlRawAsync("""
            DO $role$ BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autocheck_api') THEN
                CREATE ROLE autocheck_api NOSUPERUSER NOCREATEDB NOCREATEROLE NOLOGIN NOBYPASSRLS;
              END IF;
            END $role$;
            GRANT USAGE ON SCHEMA autocheck TO autocheck_api;
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO autocheck."UserAccount" ("Id", "AuthId", "Email", "Name", "Phone", "Role", "Permissions", "Active")
            VALUES ({legacyUserId}, {Guid.NewGuid()}, 'upgrade@example.test', 'Upgrade', '', 'Ciudadano', ARRAY[]::text[], true);
            INSERT INTO autocheck."AppSession" ("Id", "UserId", "LastActivity", "Revoked")
            VALUES ({legacySessionId}, {legacyUserId}, {App.Clock.Now}, false);
            """);
    }

    [IntegrationFact]
    public async Task AdditiveUpgradePreservesSessionsAndGrantsRuntimeDmlWithoutDdl()
    {
        var user = await WithDb(db => db.Users.SingleAsync(x => x.Id == legacyUserId));
        var session = await WithDb(db => db.Sessions.SingleAsync(x => x.Id == legacySessionId));
        Assert.False(user.PasswordResetPending);
        Assert.Equal(0, user.SessionVersion);
        Assert.Equal(0, session.SecurityVersion);
        Assert.False(session.Revoked);
        var privileges = await WithDb(db => db.Database.SqlQueryRaw<bool>("""
            SELECT (
              has_table_privilege('autocheck_api', 'autocheck."PasswordResetOperation"', 'SELECT')
              AND has_table_privilege('autocheck_api', 'autocheck."PasswordResetOperation"', 'INSERT')
              AND has_table_privilege('autocheck_api', 'autocheck."PasswordResetOperation"', 'UPDATE')
              AND NOT has_table_privilege('autocheck_api', 'autocheck."PasswordResetOperation"', 'DELETE')
              AND NOT has_schema_privilege('autocheck_api', 'autocheck', 'CREATE')
            ) AS "Value"
            """).SingleAsync());
        Assert.True(privileges);
    }
}
