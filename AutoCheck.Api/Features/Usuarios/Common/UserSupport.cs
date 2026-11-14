using AutoCheck.Api.Features.Usuarios;
using AutoCheck.Api.Features.Usuarios.Common;

namespace AutoCheck.Api.Features.Usuarios.Common;

public static class UserSupport
{
    public static UserResponse Response(UserAccount u) => new(u.Id, u.Email, u.Name, u.Phone, u.Role, u.Permissions, u.CenterId, u.Active);
}
