using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo;

public interface IData
{
    Task<Snapshot?> Read(Query query, CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public async Task<VehicleResponse> Execute(Query query, CancellationToken ct)
    {
        var snapshot = await data.Read(query, ct) ?? throw ApiContract.Missing();
        if (!snapshot.CanRead)
        {
            AppActor.Deny();
        }
        return snapshot.Response;
    }
}
