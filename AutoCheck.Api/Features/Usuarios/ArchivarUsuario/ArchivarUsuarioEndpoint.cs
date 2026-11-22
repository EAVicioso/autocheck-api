// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Usuarios.ArchivarUsuario;

public static class ArchivarUsuarioEndpoint
{
    public static void MapArchivarUsuario(this IEndpointRouteBuilder endpoints) => endpoints.MapDelete("/{id:guid}", HandleAsync).RequireAuthorization("GestionUsuarios").WithMetadata(new MutationAudit("USUARIO_ARCHIVADO", SkipUnchanged: true)).WithName("ArchivarUsuario").WithSummary("DEC-LIFECYCLE-01 / RS-03").Produces(204).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, long version, HttpContext http, Handler handler, CancellationToken ct)
    {
        _ = await handler.Execute(new Command(id, version), http, ct);
        return TypedResults.NoContent();
    }
}
