namespace AutoCheck.Api.Infrastructure.Data;
// Explicit operator command, never an HTTP endpoint. All probe writes roll back.
public static class DatabaseConnectivity
{
    public static async Task<DatabaseConnectivityResult> Verify(AutoCheckDb db, CancellationToken ct)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
        if (pending.Length != 0)
        {
            throw new InvalidOperationException("Apply the reviewed pending database migrations before developing features.");
        }

        var role = await db.Database.SqlQueryRaw<string>("SELECT current_user AS \"Value\"").SingleAsync(ct);
        var canCreate = await db.Database.SqlQueryRaw<bool>("SELECT has_schema_privilege(current_user, 'autocheck', 'CREATE') AS \"Value\"").SingleAsync(ct);
        var catalogItems = await db.Catalog.AsNoTracking().CountAsync(ct);
        var rules = await db.Rules.AsNoTracking().CountAsync(ct);
        var probe = new CatalogItem
        {
            Id = Guid.NewGuid(),
            Type = "colores",
            Name = "Connectivity probe"
        };
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            db.Catalog.Add(probe);
            await db.SaveChangesAsync(ct);
            probe.Name = "Connectivity probe updated";
            await db.SaveChangesAsync(ct);
            db.Catalog.Remove(probe);
            await db.SaveChangesAsync(ct);
            await transaction.RollbackAsync(ct);
        }

        db.ChangeTracker.Clear();
        if (await db.Catalog.AnyAsync(x => x.Id == probe.Id, ct))
        {
            throw new InvalidOperationException("Connectivity probe did not roll back.");
        }

        return new(role, pending.Length, catalogItems, rules, true, canCreate);
    }
}

public sealed record DatabaseConnectivityResult(string DatabaseRole, int PendingMigrations, int CatalogItems, int RuleVersions, bool CrudRolledBack, bool CanCreateSchemaObjects);
