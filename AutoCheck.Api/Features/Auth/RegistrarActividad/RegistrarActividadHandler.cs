// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Auth.RegistrarActividad;

public interface IData
{
    Task Update(CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public Task Execute(CancellationToken ct) => data.Update(ct);
}
