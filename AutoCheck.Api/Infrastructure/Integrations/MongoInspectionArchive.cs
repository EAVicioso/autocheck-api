// AutoCheck / INTEC, SRS 1.1; Section C / PA-05/08/09/10. Version 1.0; history: Git.
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
        if (!ObjectId.TryParse(inspection.MongoId, out var id) || inspection.Defects is null)
        {
            throw new JsonException("Invalid inspection projection payload.");
        }

        var defects = JsonSerializer.Deserialize<List<ReportDefect>>(inspection.Defects, ApiReply.JsonOptions)
            ?? throw new JsonException("Missing inspection defects.");
        await using var db = connection.CreateContext();
        var snapshot = new InspectionReport
        {
            Id = id,
            InspectionId = inspection.Id.ToString(),
            Defects = defects,
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
