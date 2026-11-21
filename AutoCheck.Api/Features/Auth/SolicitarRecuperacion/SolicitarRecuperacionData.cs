namespace AutoCheck.Api.Features.Auth.SolicitarRecuperacion;

public sealed class Data(AutoCheckDb db, TimeProvider clock, IHttpContextAccessor accessor) : IData
{
    public async Task Record(CancellationToken ct)
    {
        var http = accessor.HttpContext;
        db.Audit.Add(new() { At = clock.GetUtcNow(), Operation = "RECUPERACION_SOLICITADA", Entity = "/api/auth/recuperacion", Outcome = "SOLICITADO", Correlation = http?.TraceIdentifier ?? "process", Ip = http?.Connection.RemoteIpAddress });
        await db.SaveChangesAsync(ct);
    }
}
