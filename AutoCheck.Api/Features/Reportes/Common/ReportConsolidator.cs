namespace AutoCheck.Api.Features.Reportes.Common;

public sealed class ReportConsolidator(AutoCheckDb db, TimeProvider clock)
{
    public async Task Run(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("reports", ct);
        var now = clock.GetUtcNow();
        // Closed inspections are immutable. Aggregate in PostgreSQL without materializing history
        // or deleting/reinserting metrics; a full reconciliation also includes late commits.
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO autocheck."DailyMetric" ("Day", "CenterId", "RampId", "InspectorId", "Approved", "Rejected", "Updated")
            SELECT ("Closed" AT TIME ZONE 'America/Santo_Domingo')::date, "CenterId", "RampId", "InspectorId",
                   count(*) FILTER (WHERE "Result" = 'Aprobado')::int,
                   count(*) FILTER (WHERE "Result" = 'Rechazado')::int, {now}
            FROM autocheck."Inspection" WHERE "Closed" IS NOT NULL
            GROUP BY 1, 2, 3, 4
            ON CONFLICT ("Day", "CenterId", "RampId", "InspectorId") DO UPDATE
            SET "Approved" = EXCLUDED."Approved", "Rejected" = EXCLUDED."Rejected", "Updated" = EXCLUDED."Updated"
            """, ct);
        await db.Database.ExecuteSqlAsync($"""
            WITH windows AS (
                SELECT "Id", "InspectorId", "CenterId", "Closed",
                       count(*) OVER recent AS size,
                       count(*) FILTER (WHERE "Result" = 'Aprobado') OVER recent AS approved,
                       min("Closed") OVER recent AS first_close,
                       array_agg("Id") OVER recent AS inspections,
                       array_agg("RampId") OVER recent AS ramps
                FROM autocheck."Inspection" WHERE "Closed" IS NOT NULL
                WINDOW recent AS (PARTITION BY "InspectorId", "CenterId" ORDER BY "Closed", "Id"
                                  ROWS BETWEEN 20 PRECEDING AND CURRENT ROW)
            )
            INSERT INTO autocheck."AnomalyAlert" ("Id", "InspectorId", "CenterId", "FinalInspectionId", "Day",
                                                 "From", "To", "Approved", "InspectionIds", "RampIds")
            SELECT gen_random_uuid(), "InspectorId", "CenterId", "Id",
                   ("Closed" AT TIME ZONE 'America/Santo_Domingo')::date,
                   first_close, "Closed", approved::int, inspections,
                   ARRAY(SELECT DISTINCT ramp FROM unnest(ramps) AS ramp ORDER BY ramp)
            FROM windows WHERE size = 21 AND approved >= 20
            ON CONFLICT ("InspectorId", "CenterId", "FinalInspectionId") DO NOTHING
            """, ct);
        var state = await db.Aggregation.FindAsync([1], ct);
        if (state is null)
        {
            state = new();
            db.Aggregation.Add(state);
        }

        state.Updated = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
