// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace AutoCheck.Tests.Integration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SupabaseFactAttribute : FactAttribute
{
    public SupabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AUTOCHECK_SUPABASE_E2E") != "true")
        {
            Skip = "Opt-in hosted HTTP test: configure local secrets and AUTOCHECK_SUPABASE_E2E=true.";
        }
    }
}

// Never inherits DatabaseTest: no database creation, migrations, physical cleanup or resets.
[Collection("Supabase hosted")]
[Trait("Category", "SupabaseE2E")]
public sealed class SupabaseLifecycleTests
{
    [SupabaseFact]
    public async Task RealAuthAndVehicleLifecyclePersistWithImmutableAudit()
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        Assert.Equal("https://odrsvktbllsfdqyazxsg.supabase.co", configuration["Supabase:Url"]?.TrimEnd('/'));
        SupabaseScenario.AssertTarget(configuration);
        var email = configuration["SupabaseE2E:Email"] ?? throw new InvalidOperationException("SupabaseE2E:Email is required.");
        var password = configuration["SupabaseE2E:Password"] ?? throw new InvalidOperationException("SupabaseE2E:Password is required.");
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development")
            .ConfigureAppConfiguration((_, c) => c.AddConfiguration(configuration).AddInMemoryCollection(new Dictionary<string, string?> { ["Workers:Enabled"] = "false" })));
        app.UseKestrel(0);
        await SupabaseScenario.VerifyRuntimePostgres(app.Services);
        using var client = app.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { correo = email, contrasena = password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var tokens = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.RootElement.GetProperty("accessToken").GetString());
        var nonce = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var request = new
        {
            matricula = "E2E" + nonce[..8],
            vin = nonce[..17],
            anio = 2020,
            modeloId = Guid.Parse("20000000-0000-0000-0000-000000000002"),
            colorId = Guid.Parse("20000000-0000-0000-0000-000000000003")
        };
        using var created = await client.PostAsJsonAsync("/api/vehiculos", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        var version = body.RootElement.GetProperty("version").GetInt64();
        Assert.Equal($"/api/vehiculos/{id}", created.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/vehiculos/{id}")).StatusCode);
        using var updated = await client.PatchAsJsonAsync($"/api/vehiculos/{id}", new { request.matricula, request.vin, anio = 2021, request.modeloId, request.colorId, version });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var archived = await client.DeleteAsync($"/api/vehiculos/{id}?version={version + 1}");
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoCheckDb>();
        var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.NotNull(vehicle.ArchivedAt);
        Assert.Equal(2021, vehicle.Year);
        Assert.True(await db.Audit.AnyAsync(x => x.Entity == $"/api/vehiculos/{id}" && x.Operation == "VEHICULO_ARCHIVADO"));
        // Retain the tombstone and audit evidence. Never physically clean up shared history.
    }
}
