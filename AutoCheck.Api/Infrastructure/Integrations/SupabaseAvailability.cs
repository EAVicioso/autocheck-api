// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AutoCheck.Api.Infrastructure;

public interface IAvailabilityPublisher
{
    Task Publish(Guid centerId, CancellationToken ct);
}

public sealed class SupabaseAvailability(HttpClient http, IConfiguration config) : IAvailabilityPublisher
{
    public async Task Publish(Guid centerId, CancellationToken ct)
    {
        var url = config["Supabase:Url"];
        var key = config["Supabase:SecretKey"];
        if (string.IsNullOrWhiteSpace(key)
            || !Uri.TryCreate(url, UriKind.Absolute, out var origin)
            || origin.Scheme != Uri.UriSchemeHttps
            || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0)
        {
            throw ApiContract.Unavailable();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "/realtime/v1/api/broadcast"));
        request.Headers.Add("apikey", key);
        if (!key.StartsWith("sb_secret_", StringComparison.Ordinal))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        request.Content = JsonContent.Create(new { messages = new[] { new { topic = "disponibilidad:" + centerId, @event = "actualizada", payload = new { centroId = centerId } } } });
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
