using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;

namespace AutoCheck.Api.Features.Usuarios.ActualizarPerfil;

public static class ActualizarPerfilEndpoint
{
    public static void MapActualizarPerfil(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/me", async (ProfileRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            var u = await db.Users.SingleAsync(x => x.Id == actor.Id, ct);
            u.Name = r.Nombre.Trim();
            u.Phone = r.Celular.Trim();
            return ApiReply.Ok(Response(u));
        })).Interactive().Document<UserResponse>("ActualizarPerfil", "SRS § 6.2.1.1").Validated<ProfileRequest>(v =>
        {
            v.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120);
            v.RuleFor(x => x.Celular).NotNull().MaximumLength(30);
        });
    }
}

public sealed record ProfileRequest(string Nombre, string Celular);
