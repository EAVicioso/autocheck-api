using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Security;
using AutoCheck.Api.Features.Usuarios.Common;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Archive = AutoCheck.Api.Features.Vehiculos.ArchivarVehiculo;
using Profile = AutoCheck.Api.Features.Usuarios.ActualizarPerfil;
using Access = AutoCheck.Api.Features.Permisos.CambiarAcceso;
using Certificate = AutoCheck.Api.Features.Certificados.SolicitarCertificado;
using Pdf = AutoCheck.Api.Features.Certificados.DescargarCertificado;
namespace AutoCheck.Tests.Features;

public sealed class LifecycleMutationHandlerTests
{
    [Theory]
    [InlineData(false, false, "ACCESO_DENEGADO")]
    [InlineData(true, true, "OPERACIONES_PENDIENTES")]
    public async Task ArchiveGuardsExecuteAgainstLockedSnapshot(bool owns, bool pending, string code)
    {
        var actor = new AppActor { Id = Guid.NewGuid() };
        var data = Substitute.For<Archive.IData>();
        data.Execute(Arg.Any<Guid>(), 1, Arg.Any<HttpContext>(), Arg.Any<Action<Archive.Snapshot>>(), default).Returns(call =>
        {
            call.Arg<Action<Archive.Snapshot>>()(new(owns ? actor.Id : Guid.NewGuid(), 1, null, pending));
            return Task.FromResult((204, (object?)null, (string?)null));
        });
        var error = await Assert.ThrowsAsync<ApiException>(() => new Archive.Handler(data, actor).Execute(new(Guid.NewGuid(), 1), new DefaultHttpContext(), default));
        Assert.Equal(code, error.Code);
    }
    [Fact]
    public async Task ProfileRejectsStaleSnapshotBeforeWriting()
    {
        var data = Substitute.For<Profile.IData>();
        data.Execute(Arg.Any<Profile.ProfileRequest>(), Arg.Any<HttpContext>(), Arg.Any<Action<Profile.Snapshot>>(), default).Returns(call =>
        {
            call.Arg<Action<Profile.Snapshot>>()(new(2, null));
            return Task.FromResult((200, (UserResponse?)null, (string?)null));
        });
        var error = await Assert.ThrowsAsync<ApiException>(() => new Profile.Handler(data).Execute(new("Ana", "", 1), new DefaultHttpContext(), default));
        Assert.Equal("VERSION_DESACTUALIZADA", error.Code);
    }
    [Fact]
    public async Task LastGlobalAdministratorCannotLoseItsPermission()
    {
        var actor = new AppActor { Id = Guid.NewGuid(), Role = Roles.Admin, Granted = [Permissions.Users] };
        var id = Guid.NewGuid();
        var data = Substitute.For<Access.IData>();
        data.Execute(id, Arg.Any<Access.AccessRequest>(), Arg.Any<HttpContext>(), Arg.Any<Action<Access.Snapshot>>(), default).Returns(call =>
        {
            var user = new UserResponse(id, "admin@example.test", "", "", Roles.Admin, [Permissions.Users], null, true, 1, null);
            call.Arg<Action<Access.Snapshot>>()(new(user, true, false, true));
            return Task.FromResult((200, (UserResponse?)null, (string?)null));
        });
        var request = new Access.AccessRequest(Roles.Citizen, [], null, true, 1);
        var error = await Assert.ThrowsAsync<ApiException>(() => new Access.Handler(data, actor).Execute(id, request, new DefaultHttpContext(), default));
        Assert.Equal("ULTIMO_ADMINISTRADOR", error.Code);
    }
    [Fact]
    public async Task RejectedInspectionCannotRequestAnApprovalCertificate()
    {
        var actor = new AppActor { Id = Guid.NewGuid() };
        var data = Substitute.For<Certificate.IData>();
        data.Execute(Arg.Any<Guid>(), Arg.Any<HttpContext>(), Arg.Any<Action<Certificate.Snapshot>>(), default).Returns(call =>
        {
            call.Arg<Action<Certificate.Snapshot>>()(new(actor.Id, "CERRADA", "Rechazado"));
            return Task.FromResult((202, (AutoCheck.Api.Features.Certificados.Common.CertificateResponse?)null, (string?)null));
        });
        var error = await Assert.ThrowsAsync<ApiException>(() => new Certificate.Handler(data, actor).Execute(new(Guid.NewGuid()), new DefaultHttpContext(), default));
        Assert.Equal("INSPECCION_NO_APROBADA", error.Code);
    }
    [Fact]
    public async Task UnauthorizedPdfNeverLoadsBinaryContent()
    {
        var data = Substitute.For<Pdf.IData>();
        data.Read(Arg.Any<Pdf.Query>(), default).Returns(new Pdf.Metadata(false, "EMITIDO"));
        await Assert.ThrowsAsync<ApiException>(() => new Pdf.Handler(data).Execute(new(Guid.NewGuid()), default));
        await data.DidNotReceive().ReadPdf(Arg.Any<Pdf.Query>(), Arg.Any<CancellationToken>());
    }
}
