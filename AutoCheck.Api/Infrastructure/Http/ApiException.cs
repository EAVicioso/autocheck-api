using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public sealed class ApiException(int status, string code, string message) : Exception(message)
{
    public ApiException(int status, ApiErrorCode code, string message) : this(status, code.Wire(), message) { }
    public int Status { get; } = status;
    public string Code { get; } = code;
}
