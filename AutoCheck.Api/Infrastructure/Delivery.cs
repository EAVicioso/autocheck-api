// AutoCheck academic prototype, SRS 1.1; IS-04, IC-03, RD-11.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AutoCheck.Api.Infrastructure;

public interface IInspectionArchive { Task Copy(Inspection inspection, CancellationToken ct); }
public sealed class MongoInspectionArchive(IConfiguration config) : IInspectionArchive
{
    public async Task Copy(Inspection i, CancellationToken ct)
    {
        var connection = config["Mongo:ConnectionString"]; if (string.IsNullOrWhiteSpace(connection)) throw ApiContract.Unavailable();
        var settings = MongoClientSettings.FromConnectionString(connection);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2); settings.ConnectTimeout = TimeSpan.FromSeconds(2); settings.SocketTimeout = TimeSpan.FromSeconds(3);
        var collection = new MongoClient(settings).GetDatabase("autocheck_inspections_db").GetCollection<BsonDocument>("reportes_defectos");
        var snapshot = new BsonDocument { ["_id"] = ObjectId.Parse(i.MongoId), ["inspeccionId"] = i.Id.ToString(), ["defectos"] = BsonDocument.Parse("{\"items\":" + i.Defects + "}")["items"],
            ["resultado"] = i.Result, ["L"] = i.Minor, ["Md"] = i.Medium, ["G"] = i.Serious, ["M"] = i.MaxMinor, ["versionReglas"] = i.RuleVersion };
        await collection.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(i.MongoId)), snapshot, new ReplaceOptions { IsUpsert = true }, ct);
    }
}

public interface IAppointmentNotifications { Task Send(Appointment appointment, string kind, Guid deliveryId, CancellationToken ct); }
public sealed class AppointmentNotifications(AutoCheckDb db, IConfiguration config, IHttpClientFactory factory) : IAppointmentNotifications
{
    public async Task Send(Appointment appointment, string kind, Guid deliveryId, CancellationToken ct)
    {
        var key = config["SendGrid:ApiKey"]; var sender = config["SendGrid:From"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(sender)) throw ApiContract.Unavailable();
        var email = await db.Users.Where(x => x.Id == appointment.UserId).Select(x => x.Email).SingleAsync(ct);
        var message = MailHelper.CreateSingleEmail(new EmailAddress(sender, "AutoCheck"), new EmailAddress(email),
            kind == "CITA_CONFIRMADA" ? "Cita confirmada - AutoCheck" : "Cita cancelada - AutoCheck",
            $"Cita {appointment.Id}. Fecha local: {TimeZoneInfo.ConvertTime(appointment.Starts, DominicanTime.Zone):yyyy-MM-dd HH:mm}. Operación: {kind}. Pago simulado, sin movimientos reales.", null);
        message.AddCustomArg("autocheckDeliveryId", deliveryId.ToString());
        using var http = factory.CreateClient("sendgrid");
        var client = new SendGridClient(http, new SendGridClientOptions { ApiKey = key });
        var response = await client.SendEmailAsync(message, ct); if (!response.IsSuccessStatusCode) throw ApiContract.Unavailable();
    }
}

public interface IAvailabilityPublisher { Task Publish(Guid centerId, CancellationToken ct); }
public sealed class SupabaseAvailability(HttpClient http, IConfiguration config) : IAvailabilityPublisher
{
    public async Task Publish(Guid centerId, CancellationToken ct)
    {
        var url = config["Supabase:Url"]; var key = config["Supabase:SecretKey"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)) throw ApiContract.Unavailable();
        using var request = new HttpRequestMessage(HttpMethod.Post, url.TrimEnd('/') + "/realtime/v1/api/broadcast");
        request.Headers.Add("apikey", key);
        if (!key.StartsWith("sb_secret_", StringComparison.Ordinal)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(new { messages = new[] { new { topic = "disponibilidad:" + centerId, @event = "actualizada", payload = new { centroId = centerId } } } });
        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
    }
}
