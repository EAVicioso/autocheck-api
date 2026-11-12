using System.Net.Http.Headers;
using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AutoCheck.Api.Infrastructure;

public interface IInspectionArchive
{
    Task Copy(Inspection inspection, CancellationToken ct);
}

public sealed class MongoInspectionArchive(MongoArchiveConnection connection) : IInspectionArchive
{
    public async Task Copy(Inspection inspection, CancellationToken ct)
    {
        var collection = connection.Collection;
        var snapshot = new BsonDocument
        {
            ["_id"] = ObjectId.Parse(inspection.MongoId),
            ["inspeccionId"] = inspection.Id.ToString(),
            ["defectos"] = BsonDocument.Parse("{\"items\":" + inspection.Defects + "}")["items"],
            ["resultado"] = inspection.Result,
            ["L"] = inspection.Minor,
            ["Md"] = inspection.Medium,
            ["G"] = inspection.Serious,
            ["M"] = inspection.MaxMinor,
            ["versionReglas"] = inspection.RuleVersion
        };
        await collection.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(inspection.MongoId)), snapshot, new ReplaceOptions { IsUpsert = true }, ct);
    }
}
