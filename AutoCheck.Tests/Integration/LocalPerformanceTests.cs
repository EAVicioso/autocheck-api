// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using AutoCheck.Api.Infrastructure.Security;
using AutoCheck.Tests.Features.Certificados;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace AutoCheck.Tests.Integration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalPerformanceFactAttribute : FactAttribute
{
    public LocalPerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AUTOCHECK_LOCAL_PERFORMANCE") != "true")
        {
            Skip = "Opt-in loopback performance baseline.";
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalRs08FactAttribute : FactAttribute
{
    public LocalRs08FactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTOCHECK_RS08_K6")))
        {
            Skip = "Opt-in RS-08 loopback run with k6 and measured per-client HTTP links.";
        }
    }
}

[Trait("Category", "LocalPerformance")]
public sealed class LocalPerformanceTests(ITestOutputHelper output) : DatabaseTest
{
    protected override bool BackgroundWorkers => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTOCHECK_RS08_K6"));

    [LocalRs08Fact]
    public async Task Rs08HistoryAndPdfWithCalibratedLinks()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        var ct = timeout.Token;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var evidence = Path.Combine(root, "artifacts", "rs08-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(evidence);
        var privateData = Path.GetTempFileName();
        var profilePath = Path.Combine(evidence, "network.json");
        using var emulator = StartEmulator(root, profilePath);
        try
        {
            var proxy = await AwaitEmulator(emulator, profilePath, ct);
            var fixtures = await WithDb(db => SeedHistory(db, ct));
            var history = fixtures.Select(x => new { token = FakeIdentity.Token(x.AuthId, x.SessionId), vehiculoId = x.VehicleId });
            await File.WriteAllTextAsync(privateData, JsonSerializer.Serialize(new { records = history }), ct);
            await RunK6("history", root, evidence, profilePath, privateData, proxy, ct);
            await VerifyActiveLinks(proxy, evidence, ct);

            var ids = await WithDb(db => SeedPdfInspections(db, ct));
            var token = Citizen.DefaultRequestHeaders.Authorization!.Parameter!;
            var pdf = ids.Select(id => new { token, inspeccionId = id });
            await File.WriteAllTextAsync(privateData, JsonSerializer.Serialize(new { records = pdf }), ct);
            await RunK6("pdf", root, evidence, profilePath, privateData, proxy, ct);
            Assert.Equal(20, await WithDb(db => db.Certificates.CountAsync(
                x => ids.Contains(x.InspectionId) && x.State == "EMITIDO", ct)));
            output.WriteLine("RS-08 local evidence: " + evidence);
        }
        finally
        {
            File.Delete(privateData);
            if (!emulator.HasExited)
            {
                emulator.Kill();
                await emulator.WaitForExitAsync();
            }
        }
    }

    private Process StartEmulator(string root, string evidence)
    {
        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(Path.Combine(root, "tests", "load", "local-rs08-link.mjs"));
        info.ArgumentList.Add(Citizen.BaseAddress!.GetLeftPart(UriPartial.Authority));
        info.ArgumentList.Add(evidence);
        return Process.Start(info) ?? throw new InvalidOperationException("Could not start the RS-08 link emulator.");
    }

    private static async Task<string> AwaitEmulator(Process emulator, string evidence, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        while (!File.Exists(evidence))
        {
            Assert.False(emulator.HasExited, "The per-client calibration process exited.");
            await Task.Delay(250, timeout.Token);
        }
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(evidence, timeout.Token));
        Assert.Equal(100, json.RootElement.GetProperty("measurements").GetArrayLength());
        return json.RootElement.GetProperty("url").GetString()!;
    }

    private static async Task RunK6(string profile, string root, string evidence, string network, string privateData, string proxy, CancellationToken ct)
    {
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("AUTOCHECK_RS08_K6")!)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "run", "--quiet", "--summary-export", Path.Combine(evidence, profile + ".json"),
                     Path.Combine(root, "tests", "load", "acceptance.js") })
        {
            info.ArgumentList.Add(argument);
        }
        info.Environment["PROFILE"] = profile;
        info.Environment["BASE_URL"] = proxy;
        info.Environment["NETWORK_EVIDENCE"] = network;
        info.Environment["TEST_DATA"] = privateData;
        info.Environment["LOCAL_RS08_LINK"] = "true";
        info.Environment.Remove("K6_HTTP_DEBUG");
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start k6.");
        try
        {
            await process.WaitForExitAsync(ct);
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private static async Task VerifyActiveLinks(string proxy, string evidence, CancellationToken ct)
    {
        using var client = new HttpClient();
        var bytes = await client.GetByteArrayAsync(proxy + "/_rs08/stats", ct);
        using var activity = JsonDocument.Parse(bytes);
        Assert.Equal(100, activity.RootElement.GetArrayLength());
        Assert.All(activity.RootElement.EnumerateArray(), link => Assert.True(link.GetProperty("requests").GetInt32() > 0));
        await File.WriteAllBytesAsync(Path.Combine(evidence, "client-activity.json"), bytes, ct);
    }

    [LocalPerformanceFact]
    public async Task HistoryBaselineWith100SessionsAnd10000Inspections()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var ct = timeout.Token;
        var fixtures = await WithDb(db => SeedHistory(db, ct));
        using var process = Process.GetCurrentProcess();
        var started = Stopwatch.GetTimestamp();
        var allocationStart = GC.GetTotalAllocatedBytes();
        var batches = await Task.WhenAll(fixtures.Select((fixture, index) => ReadHistory(fixture, index, ct)));
        var samples = batches.SelectMany(x => x).ToArray();
        var failures = samples.Count(x => !x.Success);
        var successful = samples.Where(x => x.Success).ToArray();
        var elapsed = Stopwatch.GetElapsedTime(started);
        process.Refresh();
        Report("history", successful.Select(x => x.DurationMs).ToArray(), failures, elapsed);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"max_bytes={successful.Select(x => x.Bytes).DefaultIfEmpty().Max()} process_peak_working_set_mib={process.PeakWorkingSet64 / 1048576d:F1} allocated_test_and_api_mib={(GC.GetTotalAllocatedBytes() - allocationStart) / 1048576d:F1}"));
        Assert.Equal(3000, samples.Length);
        Assert.DoesNotContain(samples, x => !x.Success);
    }

    [LocalPerformanceFact]
    public async Task TwentyDistinctPdfsAreIssuedDownloadedAndVerified()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = timeout.Token;
        var inspections = await WithDb(db => SeedPdfInspections(db, ct));
        using var issuer = X509CertificateLoader.LoadPkcs12FromFile(App.PfxPath, null, X509KeyStorageFlags.EphemeralKeySet);
        var baseline = Stopwatch.GetTimestamp();
        var durations = new List<double>(inspections.Length);
        foreach (var inspection in inspections)
        {
            durations.Add(await IssueDownloadAndVerify(inspection, issuer, ct));
        }
        Report("pdf", durations, 0, Stopwatch.GetElapsedTime(baseline));
        Assert.All(durations, elapsed => Assert.True(elapsed < 10000, $"PDF took {elapsed:F0} ms on loopback."));
    }

    private async Task<double> IssueDownloadAndVerify(Guid inspection, X509Certificate2 issuer, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        var token = budget.Token;
        var started = Stopwatch.GetTimestamp();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/inspecciones/{inspection}/certificado");
        request.Headers.Add("Idempotency-Key", inspection.ToString("N"));
        using var accepted = await Citizen.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        using var pending = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
        var certificate = pending.RootElement.GetProperty("id").GetGuid();

        if (!BackgroundWorkers)
        {
            using var scope = App.Services.CreateScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>()
                .DispatchOne(token, "CERTIFICADO", inspection));
        }

        while (true)
        {
            using var response = await Citizen.GetAsync($"/api/certificados/{certificate}/pdf", token);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(token);
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Assert.True(OfflinePdfVerifier.Verify(bytes, issuer));
                return elapsed;
            }

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(TimeSpan.FromMilliseconds(50), token);
        }
    }

    private async Task<HistoryFixture[]> SeedHistory(AutoCheckDb db, CancellationToken ct)
    {
        var fixtures = new HistoryFixture[100];
        for (var userIndex = 0; userIndex < fixtures.Length; userIndex++)
        {
            var owner = new UserAccount { AuthId = Guid.NewGuid(), Email = $"load-{userIndex}@example.test" };
            var session = new AppSession { Id = Guid.NewGuid(), UserId = owner.Id, LastActivity = App.Clock.Now };
            var vehicle = new Vehicle
            {
                OwnerId = owner.Id,
                Plate = $"LOAD{userIndex:D4}",
                Vin = $"LOAD{userIndex:D13}",
                Year = 2020,
                ModelId = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                ColorId = Guid.Parse("20000000-0000-0000-0000-000000000003")
            };
            db.Users.Add(owner);
            db.Sessions.Add(session);
            db.Vehicles.Add(vehicle);
            for (var n = 0; n < 100; n++)
            {
                AddInspection(db, owner.Id, vehicle.Id, App.Clock.Now.AddMinutes(-30 * (userIndex * 100 + n + 1)));
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            fixtures[userIndex] = new(owner.AuthId, session.Id, vehicle.Id);
        }
        Assert.Equal(10000, await db.Inspections.CountAsync(ct));
        return fixtures;
    }

    private async Task<Guid[]> SeedPdfInspections(AutoCheckDb db, CancellationToken ct)
    {
        var result = Enumerable.Range(1, 20).Select(n => AddInspection(db, CitizenId, VehicleId, App.Clock.Now.AddHours(-n))).ToArray();
        await db.SaveChangesAsync(ct);
        return result;
    }

    private Guid AddInspection(AutoCheckDb db, Guid owner, Guid vehicle, DateTimeOffset closed)
    {
        var attempt = new PaymentAttempt
        {
            UserId = owner,
            VehicleId = vehicle,
            CenterId = CenterId,
            RampId = RampId,
            Starts = closed.AddMinutes(-20),
            Expires = closed.AddMinutes(-15),
            State = "APROBADO"
        };
        var appointment = new Appointment
        {
            AttemptId = attempt.Id,
            UserId = owner,
            VehicleId = vehicle,
            CenterId = CenterId,
            RampId = RampId,
            InspectorId = InspectorId,
            Starts = attempt.Starts,
            State = "COMPLETADA"
        };
        var inspection = new Inspection
        {
            AppointmentId = appointment.Id,
            OwnerId = owner,
            VehicleId = vehicle,
            CenterId = CenterId,
            RampId = RampId,
            InspectorId = InspectorId,
            Started = attempt.Starts,
            Closed = closed,
            State = "CERRADA",
            Result = "Aprobado",
            RuleVersion = "V1",
            MaxMinor = 3,
            Defects = "[]",
            VehicleSnapshot = """{"matricula":"PERF01","vin":"PERF0000000000000","anio":2020}""",
            CenterSnapshot = """{"nombre":"Centro sintético"}"""
        };
        db.Payments.Add(attempt);
        db.Appointments.Add(appointment);
        db.Inspections.Add(inspection);
        return inspection.Id;
    }

    private async Task<HistorySample[]> ReadHistory(HistoryFixture fixture, int index, CancellationToken ct)
    {
        using var client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", FakeIdentity.Token(fixture.AuthId, fixture.SessionId));
        var path = $"/api/vehiculos/{fixture.VehicleId}/historial?tamano=20";
        var samples = new HistorySample[30];
        await Task.Delay(TimeSpan.FromMilliseconds(index * 10), ct);
        for (var n = 0; n < samples.Length; n++)
        {
            samples[n] = await MeasurePage(client, path, ct);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        return samples;
    }

    private static async Task<HistorySample> MeasurePage(HttpClient client, string path, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        using var response = await client.GetAsync(path, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (response.StatusCode != HttpStatusCode.OK || bytes.Length > 100 * 1024)
        {
            return new(false, elapsed, bytes.Length);
        }
        using var page = JsonDocument.Parse(bytes);
        return new(page.RootElement.GetProperty("items").GetArrayLength() == 20, elapsed, bytes.Length);
    }

    private void Report(string profile, IReadOnlyCollection<double> samples, int failures, TimeSpan elapsed)
    {
        var sorted = samples.Order().ToArray();
        var p95 = sorted.Length == 0 ? double.NaN : sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
        var max = sorted.Length == 0 ? double.NaN : sorted[^1];
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"local_baseline={profile} samples={samples.Count} failures={failures} p95_ms={p95:F2} max_ms={max:F2} elapsed_s={elapsed.TotalSeconds:F2}"));
    }

    private sealed record HistoryFixture(Guid AuthId, Guid SessionId, Guid VehicleId);

    private sealed record HistorySample(bool Success, double DurationMs, int Bytes);
}
