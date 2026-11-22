// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo;

public interface IData
{
    Task<(int Status, VehicleResponse? Body, string? Location)> Execute(VehicleRequest r, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken);
}
public sealed class Handler(IData data, AppActor actor)
{
    public Task<(int Status, VehicleResponse? Body, string? Location)> Execute(VehicleRequest r, HttpContext h, CancellationToken cancellationToken)
    {
        actor.RequireRole(Roles.Citizen);
        return data.Execute(r, h, state => Validate(state), cancellationToken);
    }
    private void Validate(Snapshot state)
    {
        if (state.OwnerId != actor.Id || !state.ActiveOwner)
        {
            AppActor.Deny();
        }
        if (!state.CatalogValid)
        {
            throw ApiContract.Invalid("Modelo o color inválido.");
        }
    }
}
