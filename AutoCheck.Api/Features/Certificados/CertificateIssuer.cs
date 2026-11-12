// AutoCheck academic prototype, SRS 1.1; RF-CER-01/02, PA-06/07, EE-03/06.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Signatures;
using QRCoder;

namespace AutoCheck.Api.Features.Certificados;

public static class CertificateManifest
{
    // Restricted RFC 8785 schema: ASCII keys/values, UUID strings, UTC strings and bounded integers.
    // Ordinal key ordering and integer JSON serialization are canonical for every allowed field.
    public static string Create(Certificate c, Inspection i) => JsonSerializer.Serialize(new SortedDictionary<string, object>(StringComparer.Ordinal) { ["versionEsquema"] = "1", ["certificadoId"] = c.Id.ToString(), ["inspeccionId"] = i.Id.ToString(), ["vehiculoId"] = i.VehicleId.ToString(), ["centroId"] = i.CenterId.ToString(), ["inspectorId"] = i.InspectorId.ToString(), ["cierreUtc"] = i.Closed!.Value.UtcDateTime.ToString("O"), ["emisionUtc"] = c.Issued.UtcDateTime.ToString("O"), ["resultado"] = i.Result!, ["L"] = i.Minor, ["Md"] = i.Medium, ["G"] = i.Serious, ["M"] = i.MaxMinor, ["versionReglas"] = i.RuleVersion });
}

public interface IAcademicPdf
{
    Task<byte[]> Create(Certificate certificate, Inspection inspection, CancellationToken ct);
}

public sealed class AcademicFontResolver : IFontResolver
{
    public byte[] GetFont(string faceName) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "NotoSans-Regular.ttf"));
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new("NotoSans");
}

public sealed class AcademicPdf(IConfiguration config) : IAcademicPdf
{
    private static readonly object FontLock = new();
    public async Task<byte[]> Create(Certificate certificate, Inspection inspection, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = config["Certificates:PfxPath"];
        var password = config["Certificates:PfxPassword"];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw ApiContract.Unavailable();
        }

        using var signingCertificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
        if (!signingCertificate.HasPrivateKey)
        {
            throw ApiContract.Unavailable();
        }

        var publicBase = config["Certificates:PublicBaseUrl"];
        if (!Uri.TryCreate(publicBase, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != "https" && !baseUri.IsLoopback))
        {
            throw ApiContract.Unavailable();
        }

        var verifyUrl = $"{publicBase.TrimEnd('/')}/api/certificados/{certificate.Id}/verificacion?hash={certificate.ManifestHash}";
        lock (FontLock)
        {
            GlobalFontSettings.FontResolver ??= new AcademicFontResolver();
        }

        using var pdf = new PdfDocument();
        pdf.Info.Title = "AutoCheck - Certificado académico";
        pdf.Info.Author = "AutoCheck / INTEC";
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using (var graphics = XGraphics.FromPdfPage(page))
        {
            var title = new XFont("Noto Sans", 22);
            var font = new XFont("Noto Sans", 11);
            var small = new XFont("Noto Sans", 8);
            graphics.DrawString("AutoCheck", title, XBrushes.DarkBlue, new XPoint(45, 65));
            graphics.DrawString("CERTIFICADO ACADÉMICO DE APROBACIÓN", font, XBrushes.Black, new XPoint(45, 95));
            using var vehicle = JsonDocument.Parse(inspection.VehicleSnapshot);
            using var center = JsonDocument.Parse(inspection.CenterSnapshot);
            var lines = new[]
            {
                $"Número: {certificate.Id}",
                $"Emisión UTC: {certificate.Issued:O}",
                $"Resultado: {inspection.Result}",
                $"Vehículo: {vehicle.RootElement.GetProperty("matricula").GetString()} / año {vehicle.RootElement.GetProperty("anio").GetInt32()}",
                $"VIN: {vehicle.RootElement.GetProperty("vin").GetString()}",
                $"Centro: {center.RootElement.GetProperty("nombre").GetString()}",
                $"Defectos: L={inspection.Minor}, Md={inspection.Medium}, G={inspection.Serious}; M={inspection.MaxMinor}; reglas {inspection.RuleVersion}"};
            var y = 135;
            foreach (var line in lines)
            {
                graphics.DrawString(line, font, XBrushes.Black, new XPoint(45, y));
                y += 28;
            }

            graphics.DrawString("Hash SHA-256 del manifiesto:", small, XBrushes.Black, new XPoint(45, 365));
            graphics.DrawString(certificate.ManifestHash, small, XBrushes.Black, new XPoint(45, 382));
            using var qr = QRCodeGenerator.GenerateQrCode(verifyUrl, QRCodeGenerator.ECCLevel.M);
            var module = 165d / qr.ModuleMatrix.Count;
            graphics.DrawRectangle(XBrushes.White, 45, 415, 165, 165);
            for (var row = 0; row < qr.ModuleMatrix.Count; row++)
            {
                for (var col = 0; col < qr.ModuleMatrix.Count; col++)
                {
                    if (qr.ModuleMatrix[row][col])
                    {
                        graphics.DrawRectangle(XBrushes.Black, 45 + col * module, 415 + row * module, module, module);
                    }
                }
            }

            graphics.DrawString("Firma digital y manifiesto adjunto verificables sin conexión.", small, XBrushes.Black, new XPoint(45, 620));
            graphics.DrawString("Documento simulado para fines académicos. Sin validez oficial de INTRANT.", small, XBrushes.Black, new XPoint(45, 645));
        }

        using var manifest = new MemoryStream(Encoding.UTF8.GetBytes(certificate.Manifest));
        pdf.AddEmbeddedFile("manifiesto.json", manifest);
        using var hash = new MemoryStream(Encoding.UTF8.GetBytes(certificate.ManifestHash));
        pdf.AddEmbeddedFile("manifiesto.sha256", hash);
        DigitalSignatureHandler.ForDocument(pdf, new PdfSharpDefaultSigner(signingCertificate, PdfMessageDigestType.SHA256), new DigitalSignatureOptions { Reason = "Emisor académico AutoCheck; integridad del PDF y manifiesto", Location = "República Dominicana" });
        using var output = new MemoryStream();
        await pdf.SaveAsync(output, false);
        ct.ThrowIfCancellationRequested();
        return output.ToArray();
    }
}

public sealed class CertificateIssuer(AutoCheckDb db, TimeProvider clock, IAcademicPdf pdf)
{
    // The caller owns the transaction and the inspection-scoped lock.
    public async Task<Certificate> Reserve(Guid inspectionId, CancellationToken ct)
    {
        var i = await db.Inspections.SingleOrDefaultAsync(x => x.Id == inspectionId, ct) ?? throw ApiContract.Missing();
        if (i.State != "CERRADA" || i.Result != "Aprobado")
        {
            throw ApiContract.Conflict("INSPECCION_NO_APROBADA", "Solo una inspección aprobada puede generar certificado.");
        }

        var c = await db.Certificates.SingleOrDefaultAsync(x => x.InspectionId == inspectionId, ct);
        if (c is not null)
        {
            return c;
        }

        c = new()
        {
            InspectionId = i.Id,
            OwnerId = i.OwnerId,
            Issued = clock.GetUtcNow()
        };
        c.Manifest = CertificateManifest.Create(c, i);
        c.ManifestHash = ApiContract.Hash(c.Manifest);
        db.Certificates.Add(c);
        return c;
    }

    public async Task Complete(Guid inspectionId, CancellationToken ct)
    {
        var c = await Reserve(inspectionId, ct);
        if (c.State == "EMITIDO")
        {
            return;
        }

        var i = await db.Inspections.SingleAsync(x => x.Id == inspectionId, ct);
        var bytes = await pdf.Create(c, i, ct);
        c.Pdf = bytes;
        c.PdfHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        c.State = "EMITIDO";
    }
}
