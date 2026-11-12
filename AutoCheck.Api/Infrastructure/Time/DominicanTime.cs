using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public static class DominicanTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");
    public static DateOnly Date(DateTimeOffset value) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, Zone).DateTime);
    public static DateTimeOffset Start(DateOnly day) => new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone));
}
