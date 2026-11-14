// Independent CMS validation: does not invoke the PDFsharp signing implementation or the server.
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoCheck.Tests.Features.Certificados;

public static partial class OfflinePdfVerifier
{
    [GeneratedRegex(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]", RegexOptions.None, 1000)]
    private static partial Regex ByteRangePattern();

    public static bool Verify(byte[] pdf, X509Certificate2 trusted)
    {
        try
        {
            var text = Encoding.Latin1.GetString(pdf);
            var range = ByteRangePattern().Match(text);
            if (!range.Success)
            {
                return false;
            }

            var offsets = Enumerable.Range(1, 4).Select(n => int.Parse(range.Groups[n].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (offsets[0] != 0 || offsets[1] <= 0 || offsets[2] <= offsets[1] || offsets[2] + offsets[3] != pdf.Length)
            {
                return false;
            }

            var gap = text.Substring(offsets[1], offsets[2] - offsets[1]).Trim();
            if (!gap.StartsWith('<') || !gap.EndsWith('>'))
            {
                return false;
            }

            var raw = Convert.FromHexString(gap[1..^1]);
            var reader = new AsnReader(raw, AsnEncodingRules.BER);
            var encoded = reader.ReadEncodedValue().ToArray();
            var signed = pdf[..offsets[1]].Concat(pdf[offsets[2]..]).ToArray();
            var cms = new SignedCms(new ContentInfo(signed), detached: true);
            cms.Decode(encoded);
            cms.CheckSignature(verifySignatureOnly: true);
            return cms.SignerInfos.Count == 1 && cms.SignerInfos[0].Certificate!.GetPublicKey().SequenceEqual(trusted.GetPublicKey());
        }
        catch (Exception e) when (e is CryptographicException or AsnContentException or FormatException or ArgumentException)
        {
            return false;
        }
    }
}
