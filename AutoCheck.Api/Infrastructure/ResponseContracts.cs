// AutoCheck SRS 1.1. Public JSON contracts used by OpenAPI compatibility checks.
namespace AutoCheck.Api.Infrastructure;

public sealed record ItemsResponse<T>(IEnumerable<T> Items);
