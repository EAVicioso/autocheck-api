// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using AutoCheck.Api.Features.Reportes.Common;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoCheck.Tests.Integration;

public partial class BusinessFlowTests
{
    [IntegrationFact]
    public async Task PA04ReplayPreservesApprovalAndAuditsOneReversalAfterCancellation()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created);
        var attempt = hold.GetProperty("intentoId").GetGuid();
        var first = await Webhook(attempt, eventId: "original-approval");
        var appointment = first.GetProperty("citaId").GetGuid();
        var endpoint = $"/api/citas/{appointment}/cancelacion";

        await Json(await Send(Citizen, HttpMethod.Post, endpoint, new { motivo = "Prueba" }, "cancel-one"));
        await Json(await Send(Citizen, HttpMethod.Post, endpoint, new { motivo = "Prueba" }, "cancel-two"));

        var replay = await Webhook(attempt, eventId: "original-approval");
        Assert.True(System.Text.Json.JsonElement.DeepEquals(first, replay));
        var current = await Json(await Citizen.GetAsync($"/api/pagos/intentos/{attempt}"));
        Assert.Equal("REVERTIDO", current.GetProperty("estado").GetString());

        var events = await WithDb(db => db.Audit.Where(x => x.Entity == attempt.ToString()).ToListAsync());
        Assert.Single(events, x => x.Operation == "RETENCION" && x.ActorId == CitizenId);
        var reversal = Assert.Single(events, x => x.Operation == "REVERSION");
        Assert.Equal(CitizenId, reversal.ActorId);
        Assert.NotEmpty(reversal.Correlation);
        Assert.NotNull(reversal.After);
    }

    [IntegrationFact]
    public async Task PA04LateApprovalRecordsExpirationAndNextActionOnce()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created);
        var attempt = hold.GetProperty("intentoId").GetGuid();
        App.Clock.Advance(TimeSpan.FromSeconds(300));

        var first = await Webhook(attempt, eventId: "late-approval");
        var replay = await Webhook(attempt, eventId: "late-approval");
        Assert.Equal("SELECCIONAR_OTRO_HORARIO", first.GetProperty("siguienteAccion").GetString());
        Assert.True(System.Text.Json.JsonElement.DeepEquals(first, replay));
        var events = await WithDb(db => db.Audit.Where(x => x.Entity == attempt.ToString()).ToListAsync());
        var expiration = Assert.Single(events, x => x.Operation == "EXPIRACION");
        var reversal = Assert.Single(events, x => x.Operation == "REVERSION");
        Assert.Equal(reversal.Correlation, expiration.Correlation);
    }

    [IntegrationFact]
    public async Task PA02dSuccessClearsFailuresAndPasswordResetPreservesLockout()
    {
        const string email = "ciudadano@example.test";
        using var client = App.CreateClient();
        async Task<HttpStatusCode> Login(string password)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new { correo = email, contrasena = password });
            return response.StatusCode;
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await Login("Incorrecta123"));
        }

        Assert.Equal(HttpStatusCode.OK, await Login("Abc12345"));
        Assert.Equal(0, await WithDb(db => db.AccountLocks.Where(x => x.Email == email).Select(x => x.Failures).SingleAsync()));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await Login("Incorrecta123"));
        }
        var lockedUntil = await WithDb(db => db.AccountLocks.Where(x => x.Email == email).Select(x => x.LockedUntil).SingleAsync());
        Assert.NotNull(lockedUntil);

        using var reset = await client.PostAsJsonAsync("/api/auth/restablecimiento", new { token = email, nuevaContrasena = "Nueva12345" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(lockedUntil, await WithDb(db => db.AccountLocks.Where(x => x.Email == email).Select(x => x.LockedUntil).SingleAsync()));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Login("Nueva12345"));
    }

    [IntegrationFact]
    public async Task PA08ConsolidationUsesOneSnapshotAcrossMetricsAndWindows()
    {
        var firstClose = App.Clock.Now.AddHours(-8);
        for (var n = 0; n < 20; n++)
        {
            await AddReportInspection(n, true, firstClose.AddSeconds(n));
        }
        await AssertReportReconciliation(20);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        using var scope = App.Services.CreateScope();
        var consolidator = scope.ServiceProvider.GetRequiredService<ReportConsolidator>();
        await WithDb(async db =>
        {
            await using var blocker = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("""SELECT * FROM autocheck."DailyMetric" FOR UPDATE""", ct);
            var blockerPid = await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(ct);
            var pending = consolidator.Run(ct);
            await WaitForBlockedBackend(blockerPid, ct);

            await AddReportInspection(20, true, firstClose.AddSeconds(20));
            await blocker.RollbackAsync(ct);
            await pending;
            return true;
        });

        Assert.Equal(20, await WithDb(db => db.Metrics.SumAsync(x => x.Approved + x.Rejected)));
        Assert.Equal(0, await WithDb(db => db.Alerts.CountAsync()));
        await AssertReportReconciliation(21);
    }

    [IntegrationFact]
    public async Task PA08WaitingConsolidatorReconcilesAfterTheFirstReplicaCommits()
    {
        var firstClose = App.Clock.Now.AddHours(-8);
        for (var n = 0; n < 20; n++)
        {
            await AddReportInspection(n, true, firstClose.AddSeconds(n));
        }
        await AssertReportReconciliation(20);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        using var firstScope = App.Services.CreateScope();
        using var secondScope = App.Services.CreateScope();
        await WithDb(async db =>
        {
            await using var blocker = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("""SELECT * FROM autocheck."DailyMetric" FOR UPDATE""", ct);
            var blockerPid = await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(ct);
            var first = firstScope.ServiceProvider.GetRequiredService<ReportConsolidator>().Run(ct);
            var firstPid = await WaitForBlockedBackend(blockerPid, ct);
            var second = secondScope.ServiceProvider.GetRequiredService<ReportConsolidator>().Run(ct);
            await WaitForBlockedBackend(firstPid, ct);

            await AddReportInspection(20, true, firstClose.AddSeconds(20));
            await blocker.RollbackAsync(ct);
            await Task.WhenAll(first, second);
            return true;
        });

        Assert.Equal(21, await WithDb(db => db.Metrics.SumAsync(x => x.Approved + x.Rejected)));
        Assert.Equal(1, await WithDb(db => db.Alerts.CountAsync()));
    }

    private async Task<int> WaitForBlockedBackend(int blockingPid, CancellationToken ct)
    {
        while (true)
        {
            var pid = await WithDb(db => db.Database.SqlQuery<int>($"""
                SELECT pid AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND {blockingPid} = ANY(pg_blocking_pids(pid))
                """).FirstOrDefaultAsync(ct));
            if (pid != 0)
            {
                return pid;
            }
            await Task.Delay(20, ct);
        }
    }

    [IntegrationFact]
    public async Task PA09SessionDatabaseWaitIsIncludedInTheContingencyDeadline()
    {
        var watch = Stopwatch.StartNew();
        await WithDb(async db =>
        {
            await using var blocker = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("""LOCK TABLE autocheck."AppSession" IN ACCESS EXCLUSIVE MODE""");
            watch.Restart();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await Citizen.PostAsJsonAsync("/api/asistencia", new { consulta = "¿Cómo reservo una cita?" }, deadline.Token);
            var body = await Json(response, HttpStatusCode.ServiceUnavailable);
            watch.Stop();
            Assert.Equal(3, body.EnumerateObject().Count());
            Assert.Equal("El servicio de asistencia no está disponible temporalmente. Intenta de nuevo.", body.GetProperty("respuesta").GetString());
            Assert.Equal("NO_DETERMINADA", body.GetProperty("categoria").GetString());
            Assert.Equal("SERVICIO_NO_DISPONIBLE", body.GetProperty("codigo").GetString());
            Assert.Equal(0, App.Assistance.Calls);
            return true;
        });
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"La contingencia tardó {watch.Elapsed}.");
    }
}
