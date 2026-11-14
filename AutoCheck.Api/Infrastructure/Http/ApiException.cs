using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public sealed class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
