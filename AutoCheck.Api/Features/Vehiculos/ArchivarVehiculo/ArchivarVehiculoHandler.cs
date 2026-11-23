// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Vehiculos.ArchivarVehiculo;

public interface IData
{
    Task<(int Status, object? Body, string? Location)> Execute(Guid id, long version, HttpContext http, Action<Snapshot> validate, CancellationToken cancellationToken);
}
public sealed class Handler(IData data, AppActor actor)
{
    public Task<(int Status, object? Body, string? Location)> Execute(Command command, HttpContext http, CancellationToken cancellationToken) => data.Execute(command.Id, command.Version, http, state => Validate(state, command.Version), cancellationToken);
    private void Validate(Snapshot state, long version)
    {
        if (state.OwnerId != actor.Id)
        {
            AppActor.Deny();
        }
        if (state.ArchivedAt is not null)
        {
            return;
        }
        Lifecycle.RequireVersion(version, state.Version, state.ArchivedAt);
        if (state.Pending)
        {
            throw ApiContract.Conflict(ApiErrorCode.OperacionesPendientes, "Resuelve las operaciones pendientes antes de archivar.");
        }
    }
}
