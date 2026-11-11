// AutoCheck academic prototype, SRS 1.1. Composition root; business rules live in slices.
using System.Text;
using System.Threading.RateLimiting;
using AutoCheck.Api.Features.Auth;
using AutoCheck.Api.Features.Auditorias;
using AutoCheck.Api.Features.Centros;
using AutoCheck.Api.Features.Certificados;
using AutoCheck.Api.Features.Chatbot;
using AutoCheck.Api.Features.Citas;
using AutoCheck.Api.Features.Inspecciones;
using AutoCheck.Api.Features.Pagos;
using AutoCheck.Api.Features.Reportes;
using AutoCheck.Api.Features.Usuarios;
using AutoCheck.Api.Features.Vehiculos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var migrate = args.Contains("--migrate"); var bootstrap = args.Contains("--bootstrap-admin");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate" && x != "--bootstrap-admin").ToArray());
builder.Services.AddOpenApi(o => o.AddBearerContracts());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AutoCheckDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Database=autocheck"));
builder.Services.AddScoped<AppActor>(); builder.Services.AddScoped<Mutation>();
builder.Services.AddScoped<PaymentWorkflow>(); builder.Services.AddSingleton<IPaymentSandbox, AcademicPaymentSandbox>();
builder.Services.AddScoped<CertificateIssuer>(); builder.Services.AddSingleton<IAcademicPdf, AcademicPdf>();
builder.Services.AddScoped<IInspectionArchive, MongoInspectionArchive>(); builder.Services.AddScoped<IAppointmentNotifications, AppointmentNotifications>();
builder.Services.AddScoped<OutboxDispatcher>(); builder.Services.AddScoped<ReportConsolidator>(); builder.Services.AddHostedService<BackgroundDelivery>();
builder.Services.AddHttpClient<IIdentityProvider, SupabaseIdentity>(c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<IAssistanceProvider, GeminiAssistance>(c => c.Timeout = TimeSpan.FromSeconds(2));
builder.Services.AddHttpClient<IAvailabilityPublisher, SupabaseAvailability>(c => c.Timeout = TimeSpan.FromSeconds(2));
builder.Services.AddHttpClient("sendgrid", c => c.Timeout = TimeSpan.FromSeconds(3));
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 128 * 1024);
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p.WithOrigins((builder.Configuration["AllowedOrigins"] ?? "http://localhost:3000").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    var issuer = (builder.Configuration["Supabase:Url"] ?? "https://unconfigured.invalid").TrimEnd('/') + "/auth/v1";
    o.TokenValidationParameters = new TokenValidationParameters { ValidIssuer = issuer, ValidAudience = "authenticated", ValidateIssuer = true, ValidateAudience = true,
        ValidateIssuerSigningKey = true, ValidateLifetime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.FromSeconds(15) };
    if (builder.Environment.IsEnvironment("Testing"))
    {
        var key = builder.Configuration["Testing:JwtKey"] ?? throw new InvalidOperationException("Testing requires an explicit signing key.");
        o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
    else
        o.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(issuer + "/.well-known/jwks.json", new SupabaseJwks(issuer), new HttpDocumentRetriever { RequireHttps = true });
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", h => RateLimitPartition.GetFixedWindowLimiter(h.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("public", h => RateLimitPartition.GetFixedWindowLimiter(h.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
if (migrate || bootstrap)
{
    using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AutoCheckDb>();
    if (migrate) await db.Database.MigrateAsync();
    if (bootstrap)
    {
        var id = Guid.Parse(builder.Configuration["Bootstrap:AuthId"] ?? throw new InvalidOperationException("Bootstrap:AuthId is required."));
        var email = ApiContract.NormalizeEmail(builder.Configuration["Bootstrap:Email"] ?? throw new InvalidOperationException("Bootstrap:Email is required."));
        var user = await db.Users.SingleOrDefaultAsync(x => x.AuthId == id);
        if (user is null) { user = new() { AuthId = id, Email = email, Name = "Administrador" }; db.Users.Add(user); }
        user.Role = Roles.Admin; user.CenterId = null; user.Permissions = Permissions.All.ToArray();
        db.Audit.Add(new() { At = DateTimeOffset.UtcNow, Operation = "BOOTSTRAP_ADMIN", Entity = user.Id.ToString(), Outcome = "CONFIGURADO", Correlation = Guid.NewGuid().ToString() });
        await db.SaveChangesAsync();
    }
    return;
}
app.UseMiddleware<ApiErrors>();
app.UseStatusCodePages(async context =>
{
    var http = context.HttpContext;
    if (http.Request.Path == "/api/asistencia")
        await Results.Json(new AssistanceResponse("No se pudo procesar la solicitud.", "NO_DETERMINADA", "SOLICITUD_INVALIDA"), statusCode: http.Response.StatusCode).ExecuteAsync(http);
    else await Results.Problem(statusCode: http.Response.StatusCode, extensions: new Dictionary<string, object?> { ["codigo"] = "SOLICITUD_RECHAZADA", ["traceId"] = http.TraceIdentifier }).ExecuteAsync(http);
});
app.UseCors("frontend");
app.UseAuthentication(); app.UseMiddleware<SessionGuard>(); app.UseAuthorization(); app.UseRateLimiter();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing") || app.Configuration.GetValue<bool>("OpenApi:Enabled")) app.MapOpenApi();
app.MapGet("/health", (TimeProvider clock) => Results.Ok(new { status = "Healthy", timestamp = clock.GetUtcNow() })).WithName("HealthCheck");
app.MapGet("/health/ready", async (AutoCheckDb db, CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "Ready" }) : Results.StatusCode(503)).WithName("Readiness");
app.MapAuth(); app.MapUsers(); app.MapVehicles(); app.MapCenters(); app.MapAppointments(); app.MapPayments(); app.MapInspections(); app.MapCertificates(); app.MapReports(); app.MapAudit(); app.MapAssistance();
app.Run();

public partial class Program;
