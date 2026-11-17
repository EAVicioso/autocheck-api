// AutoCheck / INTEC, SRS 1.1; PA-05/10. Version 1.0; history: Git.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Features.Pagos.IniciarPago;
using AutoCheck.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Tests.Integration;

public partial class BusinessFlowTests
{
    private async Task<JsonElement> StartInspectionAt(int slot, Guid? previous = null)
    {
        var hold = await Json(await Send(Citizen, HttpMethod.Post, "/api/pagos/intentos",
            new StartPaymentRequest(VehicleId, CenterId, RampId, Starts.AddMinutes(30 * slot), previous), Guid.NewGuid().ToString()), HttpStatusCode.Created);
        var paid = await Webhook(hold.GetProperty("intentoId").GetGuid());
        var appointment = paid.GetProperty("citaId").GetGuid();
        await Json(await Send(Administrator, HttpMethod.Put, $"/api/citas/{appointment}/inspector", new { inspectorId = InspectorId }));
        var key = Guid.NewGuid().ToString();
        var started = await Json(await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, key), HttpStatusCode.Created);
        var replay = await Json(await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, key), HttpStatusCode.Created);
        Assert.Equal(started.GetRawText(), replay.GetRawText());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = Guid.NewGuid() }, key)).StatusCode);
        return started;
    }

    [IntegrationFact]
    public async Task PA05AcademicCatalogIsSeededWithParentReferences()
    {
        var categories = await WithDb(db => db.Catalog.AsNoTracking().Where(x => x.Type == "defectos-categorias-v1").ToListAsync());
        var subcategories = await WithDb(db => db.Catalog.AsNoTracking().Where(x => x.Type == "defectos-subcategorias-v1").ToListAsync());
        Assert.Equal(9, categories.Count);
        Assert.Equal(18, subcategories.Count);
        Assert.All(subcategories, sub => Assert.Contains(categories, category => category.Id == sub.ParentId
            && AutoCheck.Api.Features.Inspecciones.Common.DefectCatalog.Contains(category.Name, sub.Name)));
    }

    [IntegrationFact]
    public async Task PA05FrozenRulesCatalogAndReinspectionRemainAuthoritative()
    {
        var first = await StartInspectionAt(0);
        var id = first.GetProperty("id").GetGuid();
        await Json(await Administrator.PostAsJsonAsync("/api/configuracion/reglas-inspeccion", new { m = 4, vigenciaUtc = App.Clock.Now.AddSeconds(1) }), HttpStatusCode.Created);
        App.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(HttpStatusCode.BadRequest, (await Inspector.PutAsJsonAsync($"/api/inspecciones/{id}/registro", new { versionEsperada = 0, defectos = new[] { new { descripcion = "Faro", severidad = "Leve", cantidad = 4 } } })).StatusCode);
        await Json(await Inspector.PutAsJsonAsync($"/api/inspecciones/{id}/registro", new { versionEsperada = 0, defectos = new[] { new { descripcion = "Faro", severidad = "Leve", cantidad = 4, categoria = "ALUMBRADO", subcategoria = "FAROS" } } }));
        App.Archive.Fail = true;
        var closed = await Json(await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 1 }, "c-close"));
        Assert.Equal("Rechazado", closed.GetProperty("resultado").GetString());
        Assert.Equal(3, closed.GetProperty("m").GetInt32());
        Assert.Equal("V1", closed.GetProperty("versionReglas").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 2 }, "c-close")).StatusCode);
        var second = await StartInspectionAt(1, id);
        Assert.Equal(id, second.GetProperty("inspeccionAnteriorId").GetGuid());
        Assert.Equal("V2", second.GetProperty("versionReglas").GetString());
        Assert.Equal(4, second.GetProperty("m").GetInt32());
        Assert.Equal(1, await WithDb(db => db.Outbox.CountAsync(x => x.Kind == "COPIA_MONGO")));
        await WithDb(db => db.Users.Where(x => x.Id == InspectorId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Role, Roles.Citizen)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 1 }, "c-close")).StatusCode);
    }

    [IntegrationFact]
    public async Task PA10HistoryFiltersStartDateAndRequiresCurrentAuditPermission()
    {
        var first = await StartInspectionAt(0);
        var id = first.GetProperty("id").GetGuid();
        await Json(await Inspector.PutAsJsonAsync($"/api/inspecciones/{id}/registro", new { versionEsperada = 0, defectos = Array.Empty<object>() }));
        await Json(await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 1 }, "history-close"));
        var route = $"/api/vehiculos/{VehicleId}/historial";
        var included = await Json(await Citizen.GetAsync(route + "?desde=2026-11-11&hasta=2026-11-11&estado=CERRADA&tamano=1"));
        Assert.Single(included.GetProperty("items").EnumerateArray());
        Assert.Empty((await Json(await Citizen.GetAsync(route + "?desde=2026-11-12&hasta=2026-11-12"))).GetProperty("items").EnumerateArray());
        Assert.Empty((await Json(await Citizen.GetAsync(route + "?estado=ABIERTA"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await Citizen.GetAsync(route + "?tamano=21")).StatusCode);
        await WithDb(db => db.Users.Where(x => x.Id == AdminId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Permissions, new[] { Permissions.Users })));
        Assert.Equal(HttpStatusCode.Forbidden, (await Administrator.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Administrator.GetAsync($"/api/inspecciones/{id}")).StatusCode);
    }
}
