namespace AutoCheck.Api.Features.Vehiculos.Common;

public sealed class VehicleValidator : AbstractValidator<VehicleRequest>
{
    public VehicleValidator()
    {
        RuleFor(x => x.Matricula).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.Vin).NotEmpty().Length(17).Matches("^[A-HJ-NPR-Za-hj-npr-z0-9]+$");
        RuleFor(x => x.Anio).InclusiveBetween(1900, 2100);
        RuleFor(x => x.ModeloId).NotEmpty();
        RuleFor(x => x.ColorId).NotEmpty();
    }
}
