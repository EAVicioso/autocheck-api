using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo;

public interface IData
{
    Task<(int Status, VehicleResponse? Body, string? Location)> Execute(Guid id, VehicleRequest r, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken);
}
public sealed class Handler(IData data, AppActor actor)
{
    public Task<(int Status, VehicleResponse? Body, string? Location)> Execute(Guid id, VehicleRequest r, HttpContext h, CancellationToken cancellationToken)
    {
        return data.Execute(id, r, h, state => Validate(state, r), cancellationToken);
    }
    private void Validate(Snapshot state, VehicleRequest request)
    {
        if (state.OwnerId != actor.Id || !state.ActiveOwner)
        {
            AppActor.Deny();
        }
        Lifecycle.RequireVersion(request.Version ?? state.Version, state.Version, state.ArchivedAt);
        if (!state.CatalogValid)
        {
            throw ApiContract.Invalid("Modelo o color inválido.");
        }
    }
}
