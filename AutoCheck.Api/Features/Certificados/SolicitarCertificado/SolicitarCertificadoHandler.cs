// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.SolicitarCertificado;

public interface IData
{
    Task<(int Status, CertificateResponse? Body, string? Location)> Execute(Guid id, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken);
}
public sealed class Handler(IData data, AppActor actor)
{
    public Task<(int Status, CertificateResponse? Body, string? Location)> Execute(Command command, HttpContext h, CancellationToken cancellationToken) => data.Execute(command.Id, h, Validate, cancellationToken);
    private void Validate(Snapshot state)
    {
        if (state.OwnerId != actor.Id)
        {
            AppActor.Deny();
        }
        if (state.State != "CERRADA" || state.Result != "Aprobado")
        {
            throw ApiContract.Conflict(ApiErrorCode.InspeccionNoAprobada, "Solo una inspección aprobada puede generar certificado.");
        }
    }
}
