using AutoCheck.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DatabaseInfrastructureTests : DatabaseTest
{
    [IntegrationFact]
    public async Task ConnectivityProbeExercisesEFCRUDWithoutPersistingRows()
    {
        var before = await WithDb(db => db.Catalog.CountAsync());
        var result = await WithDb(db => DatabaseConnectivity.Verify(db, default));
        Assert.True(result.CrudRolledBack);
        Assert.Equal(0, result.PendingMigrations);
        Assert.Equal(before, await WithDb(db => db.Catalog.CountAsync()));
    }

    [IntegrationFact]
    public async Task ReadinessReturns503WhenAMigrationIsPending()
    {
        Assert.Equal(200, Assert.IsType<IStatusCodeHttpResult>(await WithDb(db => DatabaseReadiness.Check(db, default)), exactMatch: false).StatusCode);
        // The isolated database owns its ledger; shared Supabase is never used by this fixture.
        await WithDb(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261112050410_HardenDatabaseBoundary'");
            return true;
        });
        Assert.Equal(503, Assert.IsType<IStatusCodeHttpResult>(await WithDb(db => DatabaseReadiness.Check(db, default)), exactMatch: false).StatusCode);
    }
}
