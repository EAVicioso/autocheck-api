// AutoCheck academic prototype, SRS 1.1. Persistence records; never HTTP responses.
namespace AutoCheck.Api.Infrastructure.Data;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthId { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Role { get; set; } = Roles.Citizen;
    public string[] Permissions { get; set; } = [];
    public Guid? CenterId { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class AccountLock
{
    public string Email { get; set; } = "";
    public int Failures { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}

public sealed class AppSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset LastActivity { get; set; }
    public bool Revoked { get; set; }
}

public sealed class CatalogItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
}

public sealed class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Plate { get; set; } = "";
    public string Vin { get; set; } = "";
    public int Year { get; set; }
    public Guid ModelId { get; set; }
    public Guid ColorId { get; set; }
}

public sealed class Center
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Active { get; set; } = true;
}

public sealed class Ramp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
}

public sealed class CenterSchedule
{
    public Guid CenterId { get; set; }
    public int Day { get; set; }
    public int Opens { get; set; }
    public int Closes { get; set; }
}

public sealed class BookingBlock
{
    public Guid RampId { get; set; }
    public DateTimeOffset Starts { get; set; }
    public Guid AttemptId { get; set; }
    public string State { get; set; } = "RETENIDO";
    public DateTimeOffset Expires { get; set; }
}

public sealed class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid CenterId { get; set; }
    public Guid RampId { get; set; }
    public DateTimeOffset Starts { get; set; }
    public DateTimeOffset Expires { get; set; }
    public string State { get; set; } = "PENDIENTE";
    public string Scenario { get; set; } = "APROBADO";
    public Guid? PreviousInspectionId { get; set; }
    public Guid? ReversalId { get; set; }
}

public sealed class PaymentEvent
{
    public string Id { get; set; } = "";
    public Guid AttemptId { get; set; }
    public string Outcome { get; set; } = "";
}

public sealed class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttemptId { get; set; }
    public Guid UserId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid CenterId { get; set; }
    public Guid RampId { get; set; }
    public Guid? InspectorId { get; set; }
    public DateTimeOffset Starts { get; set; }
    public string State { get; set; } = "CONFIRMADA";
    public Guid? PreviousInspectionId { get; set; }
}

public sealed class InspectionRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = "";
    public int MaxMinor { get; set; }
    public DateTimeOffset Effective { get; set; }
    public Guid? ActorId { get; set; }
}

public sealed class Inspection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid CenterId { get; set; }
    public Guid RampId { get; set; }
    public Guid InspectorId { get; set; }
    public Guid? PreviousId { get; set; }
    public DateTimeOffset Started { get; set; }
    public DateTimeOffset? Closed { get; set; }
    public string State { get; set; } = "ABIERTA";
    public string? Result { get; set; }
    public string RuleVersion { get; set; } = "";
    public int MaxMinor { get; set; }
    public string? Defects { get; set; }
    public int Minor { get; set; }
    public int Medium { get; set; }
    public int Serious { get; set; }
    public int Revision { get; set; }
    public string MongoId { get; set; } = "";
    public string VehicleSnapshot { get; set; } = "";
    public string CenterSnapshot { get; set; } = "";
}

public sealed class Certificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionId { get; set; }
    public Guid OwnerId { get; set; }
    public DateTimeOffset Issued { get; set; }
    public string State { get; set; } = "PENDIENTE";
    public string Manifest { get; set; } = "";
    public string ManifestHash { get; set; } = "";
    public byte[]? Pdf { get; set; }
    public string? PdfHash { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public DateTimeOffset At { get; set; }
    public string Operation { get; set; } = "";
    public string Entity { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string Correlation { get; set; } = "";
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "";
    public Guid AggregateId { get; set; }
    public DateTimeOffset Available { get; set; }
    public DateTimeOffset? Completed { get; set; }
    public int Attempts { get; set; }
    public string Payload { get; set; } = "{}";
}

public sealed class IdempotencyRecord
{
    public string Key { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Json { get; set; } = "";
    public int Status { get; set; }
    public string? Location { get; set; }
}

public sealed class DailyMetric
{
    public DateOnly Day { get; set; }
    public Guid CenterId { get; set; }
    public Guid RampId { get; set; }
    public Guid InspectorId { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public DateTimeOffset Updated { get; set; }
}

public sealed class AnomalyAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectorId { get; set; }
    public Guid CenterId { get; set; }
    public Guid FinalInspectionId { get; set; }
    public DateOnly Day { get; set; }
    public Guid[] InspectionIds { get; set; } = [];
    public Guid[] RampIds { get; set; } = [];
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public int Approved { get; set; }
}

public sealed class AggregationState
{
    public int Id { get; set; } = 1;
    public DateTimeOffset Updated { get; set; }
}
