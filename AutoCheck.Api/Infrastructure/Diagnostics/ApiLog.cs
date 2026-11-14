namespace AutoCheck.Api.Infrastructure;
// Intentionally log only classification and correlation. Provider exceptions can
// contain SQL parameters, tokens or upstream response bodies and must not be logged.
internal static partial class ApiLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Request {TraceId} failed with {Code} ({ExceptionType})")]
    internal static partial void RequestFailed(ILogger logger, string traceId, string code, string exceptionType);
    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Outbox {DeliveryId} will retry ({ExceptionType})")]
    internal static partial void DeliveryRetry(ILogger logger, Guid deliveryId, string exceptionType);
    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Delivery lane {Kind} unavailable ({ExceptionType})")]
    internal static partial void DeliveryUnavailable(ILogger logger, string kind, string exceptionType);
    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Background processing unavailable ({ExceptionType})")]
    internal static partial void BackgroundUnavailable(ILogger logger, string exceptionType);
}
