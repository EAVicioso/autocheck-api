using AutoCheck.Api.Features.Auth.Common;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Security;
using AutoCheck.Tests.Integration;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Login = AutoCheck.Api.Features.Auth.IniciarSesion;
using Refresh = AutoCheck.Api.Features.Auth.RenovarSesion;
using Register = AutoCheck.Api.Features.Auth.RegistrarCiudadano;
using Reset = AutoCheck.Api.Features.Auth.RestablecerContrasena;
namespace AutoCheck.Tests.Features;

public sealed class IdentityHandlerTests
{
    private static SessionResponse Response => new("synthetic-access", "synthetic-refresh", DateTimeOffset.MaxValue);
    [Fact]
    public async Task LockedLoginNeverCallsProvider()
    {
        var clock = new ControlledClock();
        var data = Substitute.For<Login.IData>();
        var provider = Substitute.For<IIdentityProvider>();
        data.Execute(Arg.Any<Login.LoginRequest>(), Arg.Any<HttpContext>(), Arg.Any<Func<Login.AttemptSnapshot, CancellationToken, Task<ProviderSession?>>>(), Arg.Any<Func<int, DateTimeOffset, Login.FailureState>>(), Arg.Any<Action<Login.SecuritySnapshot>>(), default).Returns(async call =>
        {
            await call.Arg<Func<Login.AttemptSnapshot, CancellationToken, Task<ProviderSession?>>>()(new(5, clock.Now.AddMinutes(1)), default);
            return Response;
        });
        var error = await Assert.ThrowsAsync<ApiException>(() => new Login.Handler(data, provider, clock).Execute(new("User@EXAMPLE.test", "synthetic"), new DefaultHttpContext(), default));
        Assert.Equal("CUENTA_BLOQUEADA", error.Code);
        await provider.DidNotReceive().Login(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    [Theory]
    [InlineData(false, 1, 1, 0)]
    [InlineData(true, 1, 1, 401)]
    [InlineData(false, 2, 1, 401)]
    public async Task LoginUsesDurableResetAndVersionBarrier(bool reset, long version, long initial, int status)
    {
        var clock = new ControlledClock();
        var data = Substitute.For<Login.IData>();
        data.Execute(Arg.Any<Login.LoginRequest>(), Arg.Any<HttpContext>(), Arg.Any<Func<Login.AttemptSnapshot, CancellationToken, Task<ProviderSession?>>>(), Arg.Any<Func<int, DateTimeOffset, Login.FailureState>>(), Arg.Any<Action<Login.SecuritySnapshot>>(), default).Returns(call =>
        {
            var failure = call.Arg<Func<int, DateTimeOffset, Login.FailureState>>();
            Assert.Null(failure(3, clock.Now).LockedUntil);
            Assert.Equal(clock.Now.AddMinutes(15), failure(4, clock.Now).LockedUntil);
            call.Arg<Action<Login.SecuritySnapshot>>()(new(true, null, reset, version, initial));
            return Task.FromResult(Response);
        });
        var handler = new Login.Handler(data, Substitute.For<IIdentityProvider>(), clock);
        if (status == 0)
        {
            Assert.Equal(Response, await handler.Execute(new("user@example.test", "synthetic"), new DefaultHttpContext(), default));
        }
        else
        {
            var error = await Assert.ThrowsAsync<ApiException>(() => handler.Execute(new("user@example.test", "synthetic"), new DefaultHttpContext(), default));
            Assert.Equal(status, error.Status);
        }
    }
    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    public async Task RefreshHonorsExactInactivityBoundary(int minutes, bool expired)
    {
        var clock = new ControlledClock();
        var provider = Substitute.For<IIdentityProvider>();
        var data = Substitute.For<Refresh.IData>();
        var tokens = new ProviderSession(Guid.NewGuid(), Guid.NewGuid(), "synthetic-access", "rotated", 3600);
        provider.Refresh("synthetic-refresh", default).Returns(tokens);
        data.Execute(tokens, Arg.Any<HttpContext>(), Arg.Any<Action<Refresh.Snapshot>>(), default).Returns(call =>
        {
            call.Arg<Action<Refresh.Snapshot>>()(new(true, false, true, null, false, 1, 1, clock.Now.AddMinutes(-minutes)));
            return Task.FromResult(Response);
        });
        var handler = new Refresh.Handler(data, provider, clock);
        if (expired)
        {
            var error = await Assert.ThrowsAsync<ApiException>(() => handler.Execute(new("synthetic-refresh"), new DefaultHttpContext(), default));
            Assert.Equal(401, error.Status);
        }
        else
        {
            Assert.Equal(Response, await handler.Execute(new("synthetic-refresh"), new DefaultHttpContext(), default));
        }
    }
    [Fact]
    public async Task RegistrationNormalizesBeforeCreatingIdentityAndPropagatesCancellation()
    {
        var data = Substitute.For<Register.IData>();
        var provider = Substitute.For<IIdentityProvider>();
        using var cancellation = new CancellationTokenSource();
        provider.Register("user@example.test", "synthetic", cancellation.Token).Returns(new ProviderRegistration(Guid.NewGuid(), true));
        data.Execute(Arg.Any<Register.RegisterRequest>(), Arg.Any<HttpContext>(), Arg.Any<Func<CancellationToken, Task<ProviderRegistration>>>(), cancellation.Token).Returns(async call =>
        {
            var request = call.Arg<Register.RegisterRequest>();
            Assert.Equal("user@example.test", request.Correo);
            Assert.Equal("Ana", request.Nombre);
            Assert.DoesNotContain(request.Contrasena, request.ToString(), StringComparison.Ordinal);
            var registered = await call.Arg<Func<CancellationToken, Task<ProviderRegistration>>>()(cancellation.Token);
            return new Register.RegistrationResponse(registered.AuthId, "PENDIENTE");
        });
        _ = await new Register.Handler(data, provider).Execute(new("  User@Example.test  ", "synthetic", " Ana "), new DefaultHttpContext(), cancellation.Token);
        await provider.Received(1).Register("user@example.test", "synthetic", cancellation.Token);
    }
    [Fact]
    public async Task ResetCannotChangeExternalPasswordWhenDurablePrepareFails()
    {
        var provider = Substitute.For<IIdentityProvider>();
        var revocation = Substitute.For<IPasswordResetRevocation>();
        var authId = Guid.NewGuid();
        provider.ValidateRecovery("synthetic", default).Returns(authId);
        revocation.Prepare(authId, "test", default).Returns(Task.FromException<Guid>(new ApiException(409, "RECUPERACION_PENDIENTE", "Pendiente")));
        await Assert.ThrowsAsync<ApiException>(() => new Reset.RestablecerContrasenaHandler(provider, revocation).Execute(new("synthetic", "replacement"), "test", default));
        await provider.DidNotReceive().Reset(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await revocation.DidNotReceive().Finish(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
