using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using AutoCheck.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class PasswordResetFlowTests : DatabaseTest
{
    private Guid additionalSessionId;
    private static Task<HttpResponseMessage> Reset(HttpClient client, CancellationToken ct = default) =>
        client.PostAsJsonAsync("/api/auth/restablecimiento", new { token = "ciudadano@example.test", nuevaContrasena = "Nueva12345" }, ct);

    [IntegrationFact]
    public async Task DatabaseOutageAfterAuthSuccessRecoversAcrossHostRestart()
    {
        await AddPriorSession();
        App.Identity.AfterReset = _ => { App.ResetConnection.Offline = true; return Task.CompletedTask; };
        using var anonymous = App.CreateClient();
        using var response = await Reset(anonymous);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Nueva12345", App.Identity.Accounts["ciudadano@example.test"].Password);
        Assert.Equal(1, App.Identity.ResetCalls);
        App.ResetConnection.Offline = false;
        await AssertBlockedBeforeReconciliation();
        await RestartAndReconcile();
        await AssertRevokedAndOtherUserUnaffected();
    }

    [IntegrationFact]
    public async Task DeferredCommitFailureRollsBackFinalizationButPreservesDurableBarrier()
    {
        await AddPriorSession();
        await WithDb(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION autocheck.fail_reset_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW."Operation" = 'RESTABLECIMIENTO_CONTRASENA' THEN RAISE EXCEPTION 'synthetic commit failure'; END IF;
              RETURN NEW;
            END $$;
            CREATE CONSTRAINT TRIGGER fail_reset_commit AFTER INSERT ON autocheck."AuditEvent"
              DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION autocheck.fail_reset_commit();
            """));
        using var anonymous = App.CreateClient();
        using var response = await Reset(anonymous);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Nueva12345", App.Identity.Accounts["ciudadano@example.test"].Password);
        await AssertBlockedBeforeReconciliation();
        Assert.True(await WithDb(db => db.Sessions.AnyAsync(x => x.UserId == CitizenId && !x.Revoked)));
        await WithDb(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_reset_commit ON autocheck.\"AuditEvent\"; DROP FUNCTION autocheck.fail_reset_commit();"));
        await RestartAndReconcile();
        await AssertRevokedAndOtherUserUnaffected();
    }

    [IntegrationFact]
    public async Task DatabaseTimeoutAfterAuthSuccessIsRecoveredWithoutRepeatingReset()
    {
        await AddPriorSession();
        App.Identity.AfterReset = _ => { App.ResetConnection.TimeoutOnce = true; return Task.CompletedTask; };
        using var anonymous = App.CreateClient();
        using var response = await Reset(anonymous);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertBlockedBeforeReconciliation();
        await RestartAndReconcile();
        await AssertRevokedAndOtherUserUnaffected();
    }

    [IntegrationFact]
    public async Task ClientCancellationAfterBarrierDoesNotCancelAuthOrFinalization()
    {
        await AddPriorSession();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        App.Identity.BeforeReset = async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); };
        using var anonymous = App.CreateClient();
        using var cancellation = new CancellationTokenSource();
        var response = Reset(anonymous, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await AssertBlockedBeforeReconciliation();
        await cancellation.CancelAsync();
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await response);
        await WaitUntil(() => WithDb(db => db.PasswordResets.AnyAsync(x => x.State == PasswordResetStates.Completed)));
        Assert.Equal("Nueva12345", App.Identity.Accounts["ciudadano@example.test"].Password);
        await AssertRevokedAndOtherUserUnaffected();
    }

    [IntegrationFact]
    public async Task DefinitiveProviderRejectionCompensatesWithoutRevokingValidSessions()
    {
        App.Identity.RejectReset = true;
        using var anonymous = App.CreateClient();
        using var response = await Reset(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Inspector.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.Equal("Abc12345", App.Identity.Accounts["ciudadano@example.test"].Password);
        Assert.Equal(PasswordResetStates.Rejected, await WithDb(db => db.PasswordResets.Select(x => x.State).SingleAsync()));
        Assert.Equal(0, await WithDb(db => db.Users.Where(x => x.Id == CitizenId).Select(x => x.SessionVersion).SingleAsync()));
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        using var scope = App.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOne(default, PasswordResetStates.DeliveryKind));
    }

    [IntegrationFact]
    public async Task InvalidTokenNeverCreatesBarrierOrCallsPasswordMutation()
    {
        using var anonymous = App.CreateClient();
        using var response = await anonymous.PostAsJsonAsync("/api/auth/restablecimiento", new { token = "invalid-recovery-token", nuevaContrasena = "Nueva12345" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, App.Identity.ResetCalls);
        Assert.Equal(0, await WithDb(db => db.PasswordResets.CountAsync()));
        Assert.Equal(HttpStatusCode.OK, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
    }

    [IntegrationFact]
    public async Task UncertainAuthOutcomeRevokesConservativelyAndNeverPersistsSecrets()
    {
        App.Identity.UncertainReset = true;
        using var anonymous = App.CreateClient();
        using var response = await Reset(anonymous);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertRevokedAndOtherUserUnaffected();
        var persisted = await WithDb(async db => JsonSerializer.Serialize(new
        {
            operations = await db.PasswordResets.AsNoTracking().ToListAsync(),
            messages = await db.Outbox.AsNoTracking().ToListAsync(),
            audit = await db.Audit.AsNoTracking().ToListAsync()
        }));
        Assert.DoesNotContain("Nueva12345", persisted);
        Assert.DoesNotContain("ciudadano@example.test", persisted);
    }

    [IntegrationFact]
    public async Task LoginMintedBeforeResetCannotRegisterAfterRevocationCompletes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        App.Identity.AfterLogin = async () => { entered.SetResult(); await release.Task; };
        using var anonymous = App.CreateClient();
        var login = anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "ciudadano@example.test", contrasena = "Abc12345" });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var reset = await Reset(anonymous);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        release.SetResult();
        using var result = await login;
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        await AssertRevokedAndOtherUserUnaffected();
    }

    [IntegrationFact]
    public async Task CrashBeforeExternalCallRecoversAndReconciliationIsIdempotent()
    {
        var authId = await WithDb(db => db.Users.Where(x => x.Id == CitizenId).Select(x => x.AuthId).SingleAsync());
        using (var scope = App.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PasswordResetRevocation>().Prepare(authId, "synthetic-crash", default);
        }
        Assert.Equal(0, App.Identity.ResetCalls);
        await RestartAndReconcile();
        await AssertRevokedAndOtherUserUnaffected();
        using var replayScope = App.Services.CreateScope();
        Assert.False(await replayScope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOne(default, PasswordResetStates.DeliveryKind));
        Assert.Equal(1, await WithDb(db => db.Audit.CountAsync(x => x.Operation == "RESTABLECIMIENTO_CONTRASENA")));
    }

    [IntegrationFact]
    public async Task SuccessfulResetAllowsANewSessionAtTheNewSecurityVersion()
    {
        using var anonymous = App.CreateClient();
        using var response = await Reset(anonymous);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "ciudadano@example.test", contrasena = "Nueva12345" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.Equal(1, await WithDb(db => db.Sessions.Where(x => x.UserId == CitizenId && !x.Revoked).Select(x => x.SecurityVersion).SingleAsync()));
    }

    private async Task AddPriorSession()
    {
        additionalSessionId = Guid.NewGuid();
        await WithDb(async db =>
        {
            db.Sessions.Add(new AppSession { Id = additionalSessionId, UserId = CitizenId, LastActivity = App.Clock.Now });
            return await db.SaveChangesAsync();
        });
    }

    private async Task AssertBlockedBeforeReconciliation()
    {
        Assert.True(await WithDb(db => db.Users.Where(x => x.Id == CitizenId).Select(x => x.PasswordResetPending).SingleAsync()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Inspector.GetAsync("/api/usuarios/me")).StatusCode);
        using var anonymous = App.CreateClient();
        using var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "ciudadano@example.test", contrasena = "Abc12345" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    private async Task AssertRevokedAndOtherUserUnaffected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Inspector.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.False(await WithDb(db => db.Sessions.AnyAsync(x => x.UserId == CitizenId && !x.Revoked)));
        Assert.False(await WithDb(db => db.Users.Where(x => x.Id == CitizenId).Select(x => x.PasswordResetPending).SingleAsync()));
        if (additionalSessionId != Guid.Empty)
        {
            var authId = await WithDb(db => db.Users.Where(x => x.Id == CitizenId).Select(x => x.AuthId).SingleAsync());
            using var additional = App.CreateClient();
            additional.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FakeIdentity.Token(authId, additionalSessionId));
            Assert.Equal(HttpStatusCode.Unauthorized, (await additional.GetAsync("/api/usuarios/me")).StatusCode);
        }
    }

    private async Task RestartAndReconcile()
    {
        var connection = await WithDb(db => Task.FromResult(db.Database.GetConnectionString()!));
        var citizenToken = Citizen.DefaultRequestHeaders.Authorization;
        var inspectorToken = Inspector.DefaultRequestHeaders.Authorization;
        var now = App.Clock.Now.Add(PasswordResetRevocation.RecoveryDelay).AddSeconds(1);
        var oldPfx = App.PfxPath;
        await App.DisposeAsync();
        File.Delete(oldPfx);
        App = new TestApplication(connection, workers: true);
        App.Clock.Now = now;
        App.UseKestrel(0);
        Citizen.Dispose();
        Inspector.Dispose();
        Citizen = App.CreateClient();
        Inspector = App.CreateClient();
        Citizen.DefaultRequestHeaders.Authorization = citizenToken;
        Inspector.DefaultRequestHeaders.Authorization = inspectorToken;
        await WaitUntil(() => WithDb(db => db.PasswordResets.AllAsync(x => x.State != PasswordResetStates.Pending)));
        Assert.Equal(0, App.Identity.ResetCalls);
    }

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!await condition())
        {
            await Task.Delay(25, timeout.Token);
        }
    }
}
