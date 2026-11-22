// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Certificados;
using AutoCheck.Api.Features.Certificados.VerificarCertificado;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;

namespace AutoCheck.Tests.Features.Certificados;

public sealed class ManifestIntegrityTests
{
    [Fact]
    public void PA07ManifestMatchesCanonicalVector()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var inspection = new Inspection
        {
            Id = id, VehicleId = id, CenterId = id, InspectorId = id,
            Closed = DateTimeOffset.UnixEpoch, Result = "Aprobado", RuleVersion = "V1",
            Minor = 3, Medium = 1, MaxMinor = 3
        };
        var certificate = new Certificate { Id = id, Issued = DateTimeOffset.UnixEpoch.AddSeconds(1) };
        var expected = $$"""{"G":0,"L":3,"M":3,"Md":1,"centroId":"{{id}}","certificadoId":"{{id}}","cierreUtc":"1970-01-01T00:00:00.0000000Z","emisionUtc":"1970-01-01T00:00:01.0000000Z","inspeccionId":"{{id}}","inspectorId":"{{id}}","resultado":"Aprobado","vehiculoId":"{{id}}","versionEsquema":"1","versionReglas":"V1"}""";
        Assert.Equal(expected, CertificateManifest.Create(certificate, inspection));
    }

    [Fact]
    public async Task PA07dRejectsChangedManifestWithOriginalHash()
    {
        var id = Guid.NewGuid();
        var originalHash = ApiContract.Hash("""{"M":3}""");
        var snapshot = new Snapshot(id, DateTimeOffset.UnixEpoch, """{"M":4}""",
            originalHash, "Centro de prueba", "Aprobado");
        var error = await Assert.ThrowsAsync<ApiException>(
            () => new Handler(new StoredCertificate(snapshot)).Execute(new Query(id, originalHash), default));
        Assert.Equal(409, error.Status);
        Assert.Equal("INTEGRIDAD_NO_VALIDA", error.Code);
    }

    private sealed class StoredCertificate(Snapshot snapshot) : IData
    {
        public Task<Snapshot?> Read(Query query, CancellationToken ct) =>
            Task.FromResult<Snapshot?>(snapshot.Id == query.Id ? snapshot : null);
    }
}
