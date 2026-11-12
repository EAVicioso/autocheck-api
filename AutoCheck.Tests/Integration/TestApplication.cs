// AutoCheck / INTEC, SRS 1.1. Acceptance evidence; history: Git.
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AutoCheck.Api.Features.Chatbot;
using AutoCheck.Api.Features.Chatbot.Common;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using AutoCheck.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace AutoCheck.Tests.Integration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTOCHECK_TEST_POSTGRES")) && Environment.GetEnvironmentVariable("CI") != "true")
        {
            Skip = "Requires isolated local PostgreSQL: AUTOCHECK_TEST_POSTGRES.";
        }
    }
}

public sealed class ControlledClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-11-11T12:00:00Z", global::System.Globalization.CultureInfo.InvariantCulture);

    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan interval) => Now += interval;
}

public sealed class FakeIdentity : IIdentityProvider
{
    public const string JwtKey = "autocheck-isolated-tests-only-signing-key-0123456789";
    public Dictionary<string, (Guid Id, string Password)> Accounts { get; } = [];

    public static string Token(Guid authId, Guid sessionId, string role = "Administrador") => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer: "https://example.test/auth/v1", audience: "authenticated", claims: [new("sub", authId.ToString()), new("session_id", sessionId.ToString()), new("role", role)], expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256)));
    public Task<ProviderRegistration> Register(string email, string password, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        Accounts.Add(email, (id, password));
        return Task.FromResult(new ProviderRegistration(id, false));
    }

    public Task<ProviderSession?> Login(string email, string password, CancellationToken ct)
    {
        if (!Accounts.TryGetValue(email, out var account) || account.Password != password)
        {
            return Task.FromResult<ProviderSession?>(null);
        }

        var session = Guid.NewGuid();
        return Task.FromResult<ProviderSession?>(new(account.Id, session, Token(account.Id, session), session.ToString(), 3600));
    }

    public Task<ProviderSession?> Refresh(string refreshToken, CancellationToken ct) => Task.FromResult<ProviderSession?>(null);
    public Task Recover(string email, CancellationToken ct) => Task.CompletedTask;
    public Task<Guid> Reset(string recoveryToken, string password, CancellationToken ct)
    {
        if (!Accounts.TryGetValue(recoveryToken, out var account))
        {
            throw new ApiException(401, "RECUPERACION_INVALIDA", "Token sintético inválido.");
        }

        Accounts[recoveryToken] = (account.Id, password);
        return Task.FromResult(account.Id);
    }

    public Task Logout(string accessToken, CancellationToken ct) => Task.CompletedTask;
}

public sealed class FakeArchive : IInspectionArchive
{
    public bool Fail { get; set; }
    public Dictionary<string, string> Documents { get; } = [];

    public Task Copy(Inspection inspection, CancellationToken ct)
    {
        if (Fail)
        {
            throw new HttpRequestException("induced test failure");
        }

        Documents[inspection.MongoId] = inspection.Defects!;
        return Task.CompletedTask;
    }
}

public sealed class FakeNotifications : IAppointmentNotifications
{
    public bool Fail { get; set; }

    public Task Send(Appointment appointment, string kind, Guid deliveryId, CancellationToken ct) => Fail ? Task.FromException(new HttpRequestException()) : Task.CompletedTask;
}

public sealed class FakeAvailability : IAvailabilityPublisher
{
    public Task Publish(Guid centerId, CancellationToken ct) => Task.CompletedTask;
}

public sealed class FakeAssistance : IAssistanceProvider
{
    public string Mode { get; set; } = "ok";
    public int Calls { get; private set; }

    public async Task<AssistanceResponse> Answer(string question, CancellationToken ct)
    {
        Calls++;
        if (Mode == "timeout")
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        if (Mode == "error")
        {
            throw new HttpRequestException();
        }

        return Mode == "invalid" ? new("", "UNKNOWN", "INVALID") : new("Puedes gestionar tus citas en AutoCheck.", "USO_PLATAFORMA", "OK");
    }
}

public sealed class TestApplication(string connection, bool workers = false) : WebApplicationFactory<Program>
{
    public string PfxPath { get; } = Path.Combine(Path.GetTempPath(), "autocheck-sign-" + Guid.NewGuid().ToString("N") + ".pfx");
    public ControlledClock Clock { get; } = new();
    public FakeIdentity Identity { get; } = new();
    public FakeArchive Archive { get; } = new();
    public FakeNotifications Notifications { get; } = new();
    public FakeAssistance Assistance { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = connection, ["Workers:Enabled"] = workers.ToString(), ["Testing:JwtKey"] = FakeIdentity.JwtKey, ["Supabase:Url"] = "https://example.test", ["Sandbox:WebhookSecret"] = "sandbox-for-isolated-tests-only-0123456789", ["Certificates:PfxPath"] = PfxPath, ["Certificates:PublicBaseUrl"] = "https://example.test" })).ConfigureServices(s =>
    {
        s.RemoveAll<TimeProvider>();
        s.AddSingleton<TimeProvider>(Clock);
        s.RemoveAll<IIdentityProvider>();
        s.AddSingleton<IIdentityProvider>(Identity);
        s.RemoveAll<IInspectionArchive>();
        s.AddSingleton<IInspectionArchive>(Archive);
        s.RemoveAll<IAppointmentNotifications>();
        s.AddSingleton<IAppointmentNotifications>(Notifications);
        s.RemoveAll<IAvailabilityPublisher>();
        s.AddSingleton<IAvailabilityPublisher, FakeAvailability>();
        s.RemoveAll<IAssistanceProvider>();
        s.AddSingleton<IAssistanceProvider>(Assistance);
    });
}

public abstract class DatabaseTest : IAsyncLifetime, IAsyncDisposable
{
    protected virtual bool BackgroundWorkers => false;

    private string admin = "";
    private string database = "";
    protected TestApplication App { get; set; } = null!;
    protected HttpClient Citizen { get; set; } = null!;
    protected HttpClient Inspector { get; set; } = null!;
    protected HttpClient Administrator { get; set; } = null!;
    protected Guid CitizenId { get; set; } = Guid.NewGuid();
    protected Guid InspectorId { get; set; } = Guid.NewGuid();
    protected Guid AdminId { get; set; } = Guid.NewGuid();
    protected Guid CenterId { get; set; } = Guid.NewGuid();
    protected Guid RampId { get; set; } = Guid.NewGuid();
    protected Guid VehicleId { get; set; } = Guid.NewGuid();
    protected DateTimeOffset Starts => new(App.Clock.Now.UtcDateTime.Date.AddDays(1).AddHours(14));

    public async Task InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("AUTOCHECK_TEST_POSTGRES") ?? throw new InvalidOperationException("CI must configure an isolated PostgreSQL service.");
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Host is not ("localhost" or "127.0.0.1") || !builder.Database!.StartsWith("autocheck_tests", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tests accept only loopback PostgreSQL and an autocheck_tests database prefix.");
        }

        builder.Database = "postgres";
        admin = builder.ConnectionString;
        database = "autocheck_tests_" + Guid.NewGuid().ToString("N");
        await using (var c = new NpgsqlConnection(admin))
        {
            await c.OpenAsync();
            await using var q = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", c);
            await q.ExecuteNonQueryAsync();
        }

        builder.Database = database;
        App = new(builder.ConnectionString, BackgroundWorkers);
        using (var rsa = RSA.Create(2048))
        {
            var request = new CertificateRequest("CN=AutoCheck isolated academic test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllBytesAsync(App.PfxPath, certificate.Export(X509ContentType.Pfx));
        }

        App.UseKestrel(0);
        Citizen = App.CreateClient();
        Inspector = App.CreateClient();
        Administrator = App.CreateClient();
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoCheckDb>();
        await db.Database.MigrateAsync();
        db.Centers.Add(new() { Id = CenterId, Name = "Centro sintético", Address = "Dirección de prueba" });
        db.Ramps.Add(new() { Id = RampId, CenterId = CenterId, Name = "R1" });
        db.Schedules.AddRange(Enumerable.Range(0, 7).Select(day => new CenterSchedule { CenterId = CenterId, Day = day, Opens = 480, Closes = 1080 }));
        foreach (var (id, role, email, client) in new[]
        {
            (CitizenId, Roles.Citizen, "ciudadano@example.test", Citizen),
            (InspectorId, Roles.Inspector, "inspector@example.test", Inspector),
            (AdminId, Roles.Admin, "admin@example.test", Administrator)
        }

        )
        {
            var auth = Guid.NewGuid();
            var session = Guid.NewGuid();
            db.Users.Add(new() { Id = id, AuthId = auth, Email = email, Role = role, CenterId = role == Roles.Inspector ? CenterId : null, Permissions = role == Roles.Admin ? Permissions.All : [] });
            db.Sessions.Add(new() { Id = session, UserId = id, LastActivity = App.Clock.Now });
            App.Identity.Accounts[email] = (auth, "Abc12345");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FakeIdentity.Token(auth, session));
        }

        db.Vehicles.Add(new() { Id = VehicleId, OwnerId = CitizenId, Plate = "A123456", Vin = "123456789ABCDEFGH", Year = 2020, ModelId = Guid.Parse("20000000-0000-0000-0000-000000000002"), ColorId = Guid.Parse("20000000-0000-0000-0000-000000000003") });
        await db.SaveChangesAsync();
    }

    protected async Task<T> WithDb<T>(Func<AutoCheckDb, Task<T>> action)
    {
        using var scope = App.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AutoCheckDb>());
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public async Task DisposeAsync()
    {
        Citizen?.Dispose();
        Inspector?.Dispose();
        Administrator?.Dispose();
        if (App is not null)
        {
            await App.DisposeAsync();
            File.Delete(App.PfxPath);
        }

        if (database.StartsWith("autocheck_tests_", StringComparison.Ordinal) && database.Length == "autocheck_tests_".Length + 32)
        {
            await using var c = new NpgsqlConnection(admin);
            await c.OpenAsync();
            await using var q = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", c);
            await q.ExecuteNonQueryAsync();
        }
    }
}
