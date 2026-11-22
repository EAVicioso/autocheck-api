// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using AutoCheck.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
namespace AutoCheck.Tests.Integration;

// Hosted fixtures retain immutable evidence. They never inherit the destructive DatabaseTest fixture.
public sealed class SupabaseScenario : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app;
    private readonly HttpClient authAdmin = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private readonly HttpClient authPublic = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private readonly List<HttpClient> clients = [];
    private readonly List<Guid> authIds = [];
    private readonly List<Guid> aggregates = [];
    private readonly string mailbox;
    private readonly string webhookSecret;
    private readonly bool validateAtlas;
    public string RunId { get; } = Guid.NewGuid().ToString("N")[..12];
    public X509Certificate2 SigningCertificate { get; }
    public Guid? CenterId { get; set; }

    public SupabaseScenario()
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        var url = Required(config, "Supabase:Url").TrimEnd('/');
        Assert.Equal("https://odrsvktbllsfdqyazxsg.supabase.co", url);
        AssertTarget(config);
        validateAtlas = Environment.GetEnvironmentVariable("AUTOCHECK_ATLAS_E2E") == "true";
        if (validateAtlas)
        {
            var atlas = new MongoUrl(Required(config, "MongoDb:ConnectionString"));
            Assert.True(atlas.Scheme == MongoDB.Driver.Core.Configuration.ConnectionStringScheme.MongoDBPlusSrv &&
                atlas.Server.Host == "clusterac.thvhywd.mongodb.net" && atlas.UseTls && !atlas.AllowInsecureTls,
                "The hosted Atlas fixture accepts only AutoCheck / ClusterAC via SRV and validated TLS.");
        }
        mailbox = Required(config, "SupabaseE2E:Email");
        var key = Required(config, "SupabaseE2E:AdminSecret");
        var publishable = Required(config, "Supabase:PublishableKey");
        webhookSecret = Required(config, "Sandbox:WebhookSecret");
        Assert.InRange(new System.Net.Mail.MailAddress(mailbox).User.Length, 1, 48);
        SigningCertificate = X509CertificateLoader.LoadPkcs12FromFile(Required(config, "Certificates:PfxPath"), config["Certificates:PfxPassword"], X509KeyStorageFlags.EphemeralKeySet);
        Assert.True(SigningCertificate.HasPrivateKey);
        authAdmin.BaseAddress = new Uri(url + "/auth/v1/");
        authAdmin.DefaultRequestHeaders.Add("apikey", key);
        if (!key.StartsWith("sb_secret_", StringComparison.Ordinal))
        {
            authAdmin.DefaultRequestHeaders.Authorization = new("Bearer", key);
        }
        authPublic.BaseAddress = authAdmin.BaseAddress;
        authPublic.DefaultRequestHeaders.Add("apikey", publishable);
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddConfiguration(config).AddInMemoryCollection(new Dictionary<string, string?> { ["Workers:Enabled"] = "false" })));
        app.ClientOptions.AllowAutoRedirect = false;
        app.UseKestrel(0);
    }
    public static void AssertTarget(IConfiguration config)
    {
        const string project = "odrsvktbllsfdqyazxsg";
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Required(config, "ConnectionStrings:Postgres"));
        Assert.True(connection.Host == $"db.{project}.supabase.co" ||
            connection.Host is not null && connection.Host.EndsWith(".pooler.supabase.com", StringComparison.Ordinal) &&
            connection.Username is not null && connection.Username.EndsWith("." + project, StringComparison.Ordinal),
            "The hosted harness accepts only the configured AutoCheck Supabase project.");
        Assert.Equal("postgres", connection.Database);
    }
    public static async Task VerifyRuntimePostgres(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoCheckDb>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        var role = await db.Database.SqlQueryRaw<string>("SELECT current_user AS \"Value\"").SingleAsync(ct);
        Assert.Equal("autocheck_api", role);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
    }

    public async Task Preflight()
    {
        await VerifyRuntimePostgres(app.Services);
        if (validateAtlas)
        {
            var collection = app.Services.GetRequiredService<MongoArchiveConnection>().Collection;
            _ = await collection.Find(FilterDefinition<BsonDocument>.Empty).Limit(1)
                .Project(Builders<BsonDocument>.Projection.Include("_id")).FirstOrDefaultAsync();
        }
    }

    private static string Required(IConfiguration config, string key) => config[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"Configure {key} in local protected settings.");
    private string Email(char kind)
    {
        var address = new System.Net.Mail.MailAddress(mailbox);
        return $"{address.User}+ac{RunId}{kind}@{address.Host}";
    }
    private static string Password() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aB1!";
    public HttpClient Client()
    {
        var client = app.CreateClient();
        clients.Add(client);
        return client;
    }
    public async Task<T> Db<T>(Func<AutoCheckDb, Task<T>> action)
    {
        using var scope = app.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AutoCheckDb>());
    }
    public static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path, object? body = null, HttpStatusCode status = HttpStatusCode.OK, bool idempotent = false, HttpStatusCode? alternateStatus = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        if (idempotent)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        }
        using var response = await client.SendAsync(request);
        // A shared worker may finish issuance before the explicit certificate request.
        Assert.True(response.StatusCode == status || response.StatusCode == alternateStatus,
            $"{method} {path.Split('?')[0]} returned {(int)response.StatusCode}; expected {(int)status}.");
        if (status == HttpStatusCode.NoContent)
        {
            return default;
        }
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }
    public async Task<FixtureUser> GenerateConfirmedUser(char kind)
    {
        var email = Email(kind);
        var password = Password();
        var generated = await Send(authAdmin, HttpMethod.Post, "admin/generate_link", new { type = "signup", email, password });
        var authId = generated.GetProperty("id").GetGuid();
        authIds.Add(authId);
        _ = await Send(authPublic, HttpMethod.Post, "verify", new { type = "signup", token_hash = generated.GetProperty("hashed_token").GetString() });
        return await Login(email, password);
    }
    public async Task<FixtureUser> BootstrapAdministrator()
    {
        var admin = await GenerateConfirmedUser('a');
        await Db(async db =>
        {
            var user = await db.Users.SingleAsync(x => authIds.Contains(x.AuthId) && x.Id == admin.Id);
            user.Role = Roles.Admin;
            user.Permissions = Permissions.All.ToArray();
            user.Version++;
            db.Audit.Add(new() { At = TimeProvider.System.GetUtcNow(), Operation = "E2E_BOOTSTRAP", Entity = user.Id.ToString(), Outcome = "CONFIGURADO", Correlation = RunId });
            return await db.SaveChangesAsync();
        });
        return admin;
    }
    public async Task<FixtureUser> RegisterCitizen(char kind)
    {
        var email = Email(kind);
        var password = Password();
        var client = Client();
        var registered = await Send(client, HttpMethod.Post, "/api/auth/registro", new { correo = email, contrasena = password, nombre = "E2E " + RunId }, HttpStatusCode.Created);
        var id = registered.GetProperty("usuarioId").GetGuid();
        var normalizedEmail = email.ToLowerInvariant();
        var authId = await Db(db => db.Users.Where(x => x.Id == id && x.Email == normalizedEmail).Select(x => x.AuthId).SingleAsync());
        authIds.Add(authId);
        _ = await Send(authAdmin, HttpMethod.Put, $"admin/users/{authId}", new { email_confirm = true, user_metadata = new { role = "Administrador", e2e_run = RunId } });
        return await Login(email, password);
    }
    public async Task<FixtureUser> Login(string email, string password)
    {
        var client = Client();
        var response = await Send(client, HttpMethod.Post, "/api/auth/login", new { correo = email, contrasena = password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", response.GetProperty("accessToken").GetString());
        var profile = await Send(client, HttpMethod.Get, "/api/usuarios/me");
        return new FixtureUser(client, profile.GetProperty("id").GetGuid(), email, password, response.GetProperty("refreshToken").GetString()!);
    }
    public async Task<Guid> CreateCenter(FixtureUser admin)
    {
        var value = await Send(admin.Client, HttpMethod.Post, "/api/centros", new { nombre = "E2E " + RunId, direccion = "Dirección sintética", activo = true }, HttpStatusCode.Created);
        CenterId = value.GetProperty("id").GetGuid();
        var ramps = await Send(admin.Client, HttpMethod.Post, $"/api/centros/{CenterId}/rampas", new { nombre = "E2E", activa = true }, HttpStatusCode.Created);
        var schedules = Enumerable.Range(0, 7).Select(x => new { dia = x, aperturaMinutos = 0, cierreMinutos = 1440 }).ToArray();
        _ = await Send(admin.Client, HttpMethod.Put, $"/api/centros/{CenterId}/horarios", new { horarios = schedules });
        return ramps.GetProperty("id").GetGuid();
    }
    public async Task<Guid> PaidAppointment(FixtureUser citizen, Guid vehicle, Guid ramp)
    {
        var starts = TimeProvider.System.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(14);
        var attempt = await Send(citizen.Client, HttpMethod.Post, "/api/pagos/intentos", new { vehiculoId = vehicle, centroId = CenterId, rampaId = ramp, inicioUtc = new DateTimeOffset(starts, TimeSpan.Zero), escenario = "APROBADO", demoraSegundos = 600 }, HttpStatusCode.Created, true);
        var id = attempt.GetProperty("intentoId").GetGuid();
        aggregates.Add(id);
        var body = JsonSerializer.SerializeToUtf8Bytes(new { eventoId = "e2e-" + RunId, intentoId = id, resultado = "APROBADO" });
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/pagos/webhook") { Content = new ByteArrayContent(body) };
        message.Content.Headers.ContentType = new("application/json");
        message.Headers.Add("X-Sandbox-Signature", Convert.ToHexStringLower(HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(webhookSecret), body)));
        using var response = await citizen.Client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var appointment = json.RootElement.GetProperty("citaId").GetGuid();
        aggregates.Add(appointment);
        return appointment;
    }
    public async Task DispatchCertificate(Guid inspection)
    {
        aggregates.Add(inspection);
        await DeliverOwnedMessage("CERTIFICADO", inspection);
    }

    public async Task VerifyAtlasProjection(Guid inspection)
    {
        if (!validateAtlas)
        {
            return;
        }
        await DeliverOwnedMessage("COPIA_MONGO", inspection);
        var snapshot = await Db(db => db.Inspections.AsNoTracking().SingleAsync(x => x.Id == inspection));
        var collection = app.Services.GetRequiredService<MongoArchiveConnection>().Collection;
        var document = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(snapshot.MongoId))).SingleAsync();
        Assert.Equal(inspection.ToString(), document["inspeccionId"].AsString);
        Assert.Equal(snapshot.RuleVersion, document["versionReglas"].AsString);
        Assert.Equal(snapshot.Result, document["resultado"].AsString);
        Assert.Equal(snapshot.Minor, document["L"].AsInt32);
        Assert.Equal(snapshot.Medium, document["Md"].AsInt32);
        Assert.Equal(snapshot.Serious, document["G"].AsInt32);
    }

    private async Task DeliverOwnedMessage(string kind, Guid inspection)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = budget.Token;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var scope = app.Services.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
            _ = await dispatcher.DispatchOne(ct, kind, inspection);
            var db = scope.ServiceProvider.GetRequiredService<AutoCheckDb>();
            if (await db.Outbox.AnyAsync(x => x.AggregateId == inspection && x.Kind == kind && x.Completed != null, ct))
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }
        Assert.Fail($"Fixture delivery did not complete for {kind}.");
    }

    public async Task<FixtureUser> ResetPassword(FixtureUser user)
    {
        var link = await Send(authAdmin, HttpMethod.Post, "admin/generate_link", new { type = "recovery", email = user.Email });
        var password = Password();
        _ = await Send(Client(), HttpMethod.Post, "/api/auth/restablecimiento", new { token = link.GetProperty("hashed_token").GetString(), nuevaContrasena = password }, HttpStatusCode.NoContent);
        return await Login(user.Email, password);
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var id in authIds)
            {
                _ = await Send(authAdmin, HttpMethod.Put, $"admin/users/{id}", new { ban_duration = "876000h" });
            }
            if (authIds.Count > 0)
            {
                await RetireFixtureRows();
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
            authAdmin.Dispose();
            authPublic.Dispose();
            SigningCertificate.Dispose();
            await app.DisposeAsync();
        }
    }
    private async Task RetireFixtureRows()
    {
        await Db(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var ids = await db.Users.Where(x => authIds.Contains(x.AuthId)).Select(x => x.Id).ToArrayAsync();
            var now = TimeProvider.System.GetUtcNow();
            await db.Users.Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, false).SetProperty(x => x.SessionVersion, x => x.SessionVersion + 1));
            await db.Sessions.Where(x => ids.Contains(x.UserId)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true));
            var events = await db.Outbox.Where(x => x.Completed == null && aggregates.Contains(x.AggregateId)).ToArrayAsync();
            foreach (var message in events)
            {
                message.Completed = now;
            }
            if (CenterId is { } centerId)
            {
                var marker = centerId.ToString();
                await db.Outbox.Where(x => x.Completed == null && x.Kind == "DISPONIBILIDAD" && x.Payload.Contains(marker)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Completed, now));
                var pending = await db.Appointments.AnyAsync(x => x.CenterId == centerId && (x.State == "CONFIRMADA" || x.State == "EN_INSPECCION"));
                if (!pending)
                {
                    await db.Centers.Where(x => x.Id == centerId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, false));
                }
            }
            db.Audit.Add(new() { At = now, Operation = "E2E_FIN", Entity = RunId, Outcome = "CUENTAS_INACTIVAS_EVIDENCIA_CONSERVADA", Correlation = RunId });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        });
    }
}
public sealed record FixtureUser(HttpClient Client, Guid Id, string Email, string Password, string RefreshToken)
{
    public override string ToString() => "FixtureUser [REDACTADO]";
}
