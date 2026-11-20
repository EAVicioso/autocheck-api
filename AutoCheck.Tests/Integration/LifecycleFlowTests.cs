using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class LifecycleFlowTests : DatabaseTest
{
    [IntegrationFact]
    public async Task CatalogsAndDetailsRequireDatabasePermissions()
    {
        Assert.Equal(HttpStatusCode.OK, (await Administrator.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Citizen.GetAsync("/api/permisos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Administrator.GetAsync($"/api/usuarios/{CitizenId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Administrator.GetAsync($"/api/usuarios/{CitizenId}/acceso")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Citizen.GetAsync("/api/certificados")).StatusCode);
    }
    [IntegrationFact]
    public async Task VehicleArchiveRetainsIdentityAndRejectsNewWrites()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Inspector.DeleteAsync($"/api/vehiculos/{VehicleId}?version=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Citizen.DeleteAsync($"/api/vehiculos/{VehicleId}?version=2")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Citizen.DeleteAsync($"/api/vehiculos/{VehicleId}?version=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Citizen.DeleteAsync($"/api/vehiculos/{VehicleId}?version=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Citizen.GetAsync($"/api/vehiculos/{VehicleId}/historial")).StatusCode);
        Assert.Equal(1, await WithDb(db => db.Vehicles.CountAsync(x => x.Id == VehicleId)));
        Assert.Equal(1, await WithDb(db => db.Audit.CountAsync(x => x.Operation == "VEHICULO_ARCHIVADO")));
    }
    [IntegrationFact]
    public async Task ProfileRequiresCurrentVersionAndAuditsOnce()
    {
        var payload = new { nombre = "Actualizado", celular = "8095551234", version = 1 };
        Assert.Equal(HttpStatusCode.OK, (await Citizen.PatchAsJsonAsync("/api/usuarios/me", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Citizen.PatchAsJsonAsync("/api/usuarios/me", payload)).StatusCode);
        Assert.Equal(1, await WithDb(db => db.Audit.CountAsync(x => x.Operation == "PERFIL_ACTUALIZADO")));
    }
    [IntegrationFact]
    public async Task ArchiveUserRevokesSessionsAndPreservesAuthIdentity()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await Administrator.DeleteAsync($"/api/usuarios/{CitizenId}?version=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.True(await WithDb(db => db.Users.AnyAsync(x => x.Id == CitizenId)));
        Assert.False(await WithDb(db => db.Sessions.AnyAsync(x => x.UserId == CitizenId && !x.Revoked)));
    }
    [IntegrationFact]
    public async Task ConcurrentProfileWritesOnlyCommitOneVersion()
    {
        var payload = new { nombre = "Concurrente", celular = "", version = 1 };
        var responses = await Task.WhenAll(Citizen.PatchAsJsonAsync("/api/usuarios/me", payload), Citizen.PatchAsJsonAsync("/api/usuarios/me", payload));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, await WithDb(db => db.Audit.CountAsync(x => x.Operation == "PERFIL_ACTUALIZADO")));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }
    [IntegrationFact]
    public async Task LiveReservationPreventsVehicleArchive()
    {
        Citizen.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var hold = await Citizen.PostAsJsonAsync("/api/pagos/intentos", new { vehiculoId = VehicleId, centroId = CenterId, rampaId = RampId, inicioUtc = Starts });
        Assert.Equal(HttpStatusCode.Created, hold.StatusCode);
        using var archive = await Citizen.DeleteAsync($"/api/vehiculos/{VehicleId}?version=1");
        Assert.Equal(HttpStatusCode.Conflict, archive.StatusCode);
        Assert.False(await WithDb(db => db.Vehicles.AnyAsync(x => x.Id == VehicleId && x.ArchivedAt != null)));
        Assert.False(await WithDb(db => db.Audit.AnyAsync(x => x.Operation == "VEHICULO_ARCHIVADO")));
    }
}
