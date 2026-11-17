using MongoDB.Bson;

namespace AutoCheck.Api.Infrastructure;

public interface IInspectionArchive
{
    Task Copy(Inspection inspection, CancellationToken ct);
}

public sealed class MongoInspectionArchive(MongoArchiveConnection connection) : IInspectionArchive
{
    public async Task Copy(Inspection inspection, CancellationToken ct)
    {
        await using var db = connection.CreateContext();
        var snapshot = new InspectionReport
        {
            Id = ObjectId.Parse(inspection.MongoId),
            InspectionId = inspection.Id.ToString(),
            Defects = JsonSerializer.Deserialize<List<ReportDefect>>(inspection.Defects ?? throw new InvalidOperationException("Missing closure defects."), ApiReply.JsonOptions)!,
            Result = inspection.Result!,
            Minor = inspection.Minor,
            Medium = inspection.Medium,
            Serious = inspection.Serious,
            MaxMinor = inspection.MaxMinor,
            RuleVersion = inspection.RuleVersion
        };
        await db.Upsert(snapshot, ct);
    }
}
