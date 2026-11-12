using AutoCheck.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DatabaseInfrastructureTests : DatabaseTest
{
    [IntegrationFact] public async Task Connectivity_probe_exercises_EF_CRUD_without_persisting_rows()
    {
        var before = await WithDb(db => db.Catalog.CountAsync());
        var result = await WithDb(db => DatabaseConnectivity.Verify(db, default));
        Assert.True(result.CrudRolledBack); Assert.Equal(0, result.PendingMigrations);
        Assert.Equal(before, await WithDb(db => db.Catalog.CountAsync()));
    }

    [IntegrationFact] public async Task Readiness_returns_503_when_a_migration_is_pending()
    {
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await WithDb(db => DatabaseReadiness.Check(db, default))).StatusCode);
        // The isolated database owns its ledger; shared Supabase is never used by this fixture.
        await WithDb(async db => { await db.Database.ExecuteSqlRawAsync("DELETE FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261112050410_HardenDatabaseBoundary'"); return true; });
        Assert.Equal(503, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await WithDb(db => DatabaseReadiness.Check(db, default))).StatusCode);
    }
}
