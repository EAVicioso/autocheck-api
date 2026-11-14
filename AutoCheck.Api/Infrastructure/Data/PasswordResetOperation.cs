namespace AutoCheck.Api.Infrastructure.Data;

// RS-09/10/11: a recovery intent never contains credentials or token material.
public sealed class PasswordResetOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string State { get; set; } = PasswordResetStates.Pending;
    public DateTimeOffset Started { get; set; }
    public DateTimeOffset? Finished { get; set; }
    public string Correlation { get; set; } = "";
}

public static class PasswordResetStates
{
    public const string Pending = "PENDING";
    public const string Completed = "COMPLETED";
    public const string Rejected = "REJECTED";
    public const string Uncertain = "REVOKED_UNCERTAIN";
    public const string DeliveryKind = "PASSWORD_RESET_REVOCATION";
}
