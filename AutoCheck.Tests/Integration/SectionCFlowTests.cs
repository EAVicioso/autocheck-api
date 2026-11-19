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
    private async Task<JsonElement> StartInspectionAt(int slot, Guid? previous = null, string? idempotencyKey = null)
    {
        var hold = await Json(await Send(Citizen, HttpMethod.Post, "/api/pagos/intentos",
            new StartPaymentRequest(VehicleId, CenterId, RampId, Starts.AddMinutes(30 * slot), previous), Guid.NewGuid().ToString()), HttpStatusCode.Created);
        var paid = await Webhook(hold.GetProperty("intentoId").GetGuid());
        var appointment = paid.GetProperty("citaId").GetGuid();
        await Json(await Send(Administrator, HttpMethod.Put, $"/api/citas/{appointment}/inspector", new { inspectorId = InspectorId }));
        var key = idempotencyKey ?? Guid.NewGuid().ToString();
        var started = await Json(await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, key), HttpStatusCode.Created);
        var replay = await Json(await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, key), HttpStatusCode.Created);
        Assert.Equal(started.GetRawText(), replay.GetRawText());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = Guid.NewGuid() }, key)).StatusCode);
        return started;
    }

    [IntegrationFact]
    public async Task PA05InspectorRepairsLegacyDefectsWithCatalogCodesBeforeClosing()
    {
        var inspection = await StartInspectionAt(0);
        var id = inspection.GetProperty("id").GetGuid();
        await WithDb(db => db.Inspections.Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Defects, "[{\"descripcion\":\"Faro\",\"severidad\":\"Leve\",\"cantidad\":1}]")));

        var close = $"/api/inspecciones/{id}/cierre";
        var body = new { versionEsperada = 0 };
        using var rejected = await Send(Inspector, HttpMethod.Post, close, body, "legacy-close");
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal("SOLICITUD_INVALIDA", (await Json(rejected, HttpStatusCode.BadRequest)).GetProperty("codigo").GetString());
        var unchanged = await WithDb(db => db.Inspections.AsNoTracking().SingleAsync(x => x.Id == id));
        Assert.Equal("ABIERTA", unchanged.State);
        Assert.Null(unchanged.Result);
        Assert.Null(unchanged.Closed);
        Assert.False(await WithDb(db => db.Certificates.AnyAsync(x => x.InspectionId == id)));
        Assert.False(await WithDb(db => db.Outbox.AnyAsync(x => x.AggregateId == id)));

        const string categories = "/api/catalogos/defectos-categorias-v1";
        var category = (await ReadInspectorCatalog(categories)).EnumerateArray().Single(x => x.GetProperty("nombre").GetString() == "ALUMBRADO");
        var parent = category.GetProperty("id").GetGuid();
        var subcategories = await ReadInspectorCatalog($"/api/catalogos/defectos-subcategorias-v1?padreId={parent}");
        Assert.Equal(2, subcategories.GetArrayLength());
        Assert.All(subcategories.EnumerateArray(), x => Assert.Equal(parent, x.GetProperty("padreId").GetGuid()));
        var subcategory = subcategories.EnumerateArray().Single(x => x.GetProperty("nombre").GetString() == "FAROS");
        var defects = new[] { new { categoria = category.GetProperty("nombre").GetString(), subcategoria = subcategory.GetProperty("nombre").GetString(), descripcion = "Faro", severidad = "Leve", cantidad = 1 } };
        var registration = $"/api/inspecciones/{id}/registro";
        await Json(await Inspector.PutAsJsonAsync(registration, new { versionEsperada = 0, defectos = defects }));
        var closed = await Json(await Send(Inspector, HttpMethod.Post, close, new { versionEsperada = 1 }, "legacy-close"));
        Assert.Equal("Aprobado", closed.GetProperty("resultado").GetString());
        Assert.Equal(1, await WithDb(db => db.Outbox.CountAsync(x => x.AggregateId == id && x.Kind == "CERTIFICADO")));
        Assert.Equal(HttpStatusCode.Conflict, (await Inspector.PutAsJsonAsync(registration, new { versionEsperada = 2, defectos = defects })).StatusCode);
    }

    private async Task<JsonElement> ReadInspectorCatalog(string route)
    {
        RestAssured.Dsl.Given().Header("Authorization", Inspector.DefaultRequestHeaders.Authorization!.ToString())
            .When().Get(new Uri(Inspector.BaseAddress!, route).ToString()).Then().StatusCode(200);
        return (await Json(await Inspector.GetAsync(route))).GetProperty("items");
    }

    [IntegrationFact]
    public async Task PA05ReplaysRevalidateAssignmentAndCenterWithoutRepeatingEffects()
    {
        var first = await StartInspectionAt(0, idempotencyKey: "scope-start");
        var id = first.GetProperty("id").GetGuid();
        var appointment = await WithDb(db => db.Inspections.Where(x => x.Id == id).Select(x => x.AppointmentId).SingleAsync());
        await WithDb(db => db.Appointments.Where(x => x.Id == appointment).ExecuteUpdateAsync(s => s.SetProperty(x => x.InspectorId, AdminId)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, "scope-start")).StatusCode);
        await WithDb(db => db.Appointments.Where(x => x.Id == appointment).ExecuteUpdateAsync(s => s.SetProperty(x => x.InspectorId, InspectorId)));
        await Json(await Inspector.PutAsJsonAsync($"/api/inspecciones/{id}/registro", new { versionEsperada = 0, defectos = Array.Empty<object>() }));
        await Json(await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 1 }, "scope-close"));
        var events = await WithDb(db => db.Outbox.CountAsync());
        var audits = await WithDb(db => db.Audit.CountAsync());
        await WithDb(db => db.Users.Where(x => x.Id == InspectorId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CenterId, (Guid?)null)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, "scope-start")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 1 }, "scope-close")).StatusCode);
        Assert.Equal(1, await WithDb(db => db.Inspections.CountAsync()));
        Assert.Equal(events, await WithDb(db => db.Outbox.CountAsync()));
        Assert.Equal(audits, await WithDb(db => db.Audit.CountAsync()));
    }

    [IntegrationFact]
    public async Task PA08QueryIndexesExistAfterAdditiveMigration()
    {
        var names = await WithDb(db => db.Database.SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'autocheck'").ToListAsync());
        Assert.Contains("IX_AnomalyAlert_Day_Id", names);
        Assert.Contains("IX_Inspection_Started_Id", names);
        Assert.Contains("IX_Inspection_OwnerId_Started_Id", names);
        Assert.Contains("IX_Inspection_InspectorId_CenterId_Started_Id", names);
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
