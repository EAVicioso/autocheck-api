namespace AutoCheck.Api.Features.Auth.Common;

public sealed record SessionResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiraUtc);
