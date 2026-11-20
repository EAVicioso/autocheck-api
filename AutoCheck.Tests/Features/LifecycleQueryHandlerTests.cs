using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Security;
using NSubstitute;
using Users = AutoCheck.Api.Features.Usuarios.ListarUsuarios;
using Vehicles = AutoCheck.Api.Features.Vehiculos.ListarVehiculos;
using Audit = AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;
using Catalog = AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo;
using Logout = AutoCheck.Api.Features.Auth.CerrarSesion;
namespace AutoCheck.Tests.Features;

public sealed class LifecycleQueryHandlerTests
{
    [Fact]
    public async Task UserQueryDeniesBeforeReadingData()
    {
        var data = Substitute.For<Users.IData>();
        var handler = new Users.Handler(data, new AppActor { Role = Roles.Citizen });
        await Assert.ThrowsAsync<ApiException>(() => handler.Execute(new(null, null, null, null, null, null, null, null), default));
        Assert.Empty(data.ReceivedCalls());
    }
    [Fact]
    public async Task UserQueryAppliesFiltersAndCancellation()
    {
        var data = Substitute.For<Users.IData>();
        var actor = new AppActor { Role = Roles.Admin, Granted = [Permissions.Users] };
        var handler = new Users.Handler(data, actor);
        var query = new Users.Query(Roles.Citizen, true, false, "Luis", 2, 5, "nombre", "desc");
        using var cancellation = new CancellationTokenSource();
        data.Read(query, Arg.Any<PageSettings>(), cancellation.Token).Returns(new PageResponse<AutoCheck.Api.Features.Usuarios.Common.UserResponse>([], 2, null));
        var result = await handler.Execute(query, cancellation.Token);
        Assert.Empty(result.Items);
        await data.Received(1).Read(query, Arg.Is<PageSettings>(p => p.Page == 2 && p.Size == 5 && p.Descending), cancellation.Token);
    }
    [Fact]
    public async Task InvalidVehicleOrderNeverReachesData()
    {
        var data = Substitute.For<Vehicles.IData>();
        var handler = new Vehicles.Handler(data);
        await Assert.ThrowsAsync<ApiException>(() => handler.Execute(new(null, null, null, null, "ownerId", null), default));
        Assert.Empty(data.ReceivedCalls());
    }
    [Fact]
    public async Task LocalAdministratorCannotReadGlobalAudit()
    {
        var data = Substitute.For<Audit.IData>();
        var handler = new Audit.Handler(data, new AppActor { Role = Roles.Admin, Granted = [Permissions.Audit], CenterId = Guid.NewGuid() });
        await Assert.ThrowsAsync<ApiException>(() => handler.Execute(new(null, null, null, null, null, null, null, null, null, null), default));
        Assert.Empty(data.ReceivedCalls());
    }
    [Fact]
    public async Task UnknownCatalogDoesNotQueryDatabase()
    {
        var data = Substitute.For<Catalog.IData>();
        await Assert.ThrowsAsync<ApiException>(() => new Catalog.Handler(data).Execute(new("unknown", null, null, null, null, null), default));
        Assert.Empty(data.ReceivedCalls());
    }
    [Fact]
    public async Task LogoutPersistsRevocationBeforeContactingAuth()
    {
        var data = Substitute.For<Logout.IData>();
        var auth = Substitute.For<IIdentityProvider>();
        await new Logout.Handler(data, auth).Execute(new("secret-test-value"), default);
        Received.InOrder(() => { data.Revoke(default); auth.Logout("secret-test-value", default); });
        Assert.DoesNotContain("secret-test-value", new Logout.Command("secret-test-value").ToString());
    }
}
