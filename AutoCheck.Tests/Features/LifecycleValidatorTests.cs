using FluentValidation;
using Register = AutoCheck.Api.Features.Auth.RegistrarCiudadano;
using Profile = AutoCheck.Api.Features.Usuarios.ActualizarPerfil;
using Vehicle = AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Tests.Features;

public sealed class LifecycleValidatorTests
{
    [Theory]
    [InlineData("Abc1234")]
    [InlineData("abcdefgh")]
    [InlineData("12345678")]
    public async Task RegistrationRejectsInvalidPasswords(string password)
    {
        var result = await new Register.Validator().ValidateAsync(new Register.RegisterRequest("nuevo@example.test", password, "Ana"));
        Assert.False(result.IsValid);
    }
    [Fact]
    public async Task ProfileRequiresVersionBeforeEffects()
    {
        var result = await new Profile.Validator().ValidateAsync(new Profile.ProfileRequest("Ana", "", 0));
        Assert.Contains(result.Errors, x => x.PropertyName == "Version");
    }
    [Fact]
    public async Task VehicleRejectsInvalidVin()
    {
        var result = await new Vehicle.VehicleValidator().ValidateAsync(new Vehicle.VehicleRequest("A123", "INVALID", 2020, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Contains(result.Errors, x => x.PropertyName == "Vin");
    }
}
