// AutoCheck / INTEC, SRS 1.1. Acceptance evidence; history: Git.
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AutoCheck.Tests.Integration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MongoIntegrationFactAttribute : FactAttribute
{
    public MongoIntegrationFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AUTOCHECK_TEST_MONGO")) && Environment.GetEnvironmentVariable("CI") != "true")
        {
            Skip = "Requires isolated loopback MongoDB: AUTOCHECK_TEST_MONGO.";
        }
    }
}

[Trait("Category", "Integration")]
public class MongoArchiveTests
{
    [MongoIntegrationFact]
    public async Task PA05ArchiveUpsertReusesCanonicalObjectId()
    {
        var url = Environment.GetEnvironmentVariable("AUTOCHECK_TEST_MONGO") ?? throw new InvalidOperationException("CI must configure MongoDB.");
        var mongoUrl = new MongoUrl(url);
        if (mongoUrl.Server.Host is not ("127.0.0.1" or "localhost"))
        {
            throw new InvalidOperationException("Only isolated loopback MongoDB is allowed.");
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Mongo:ConnectionString"] = url }).Build();
        using var connection = new MongoArchiveConnection(config);
        var archive = new MongoInspectionArchive(connection);
        var id = ObjectId.GenerateNewId();
        var inspection = new Inspection
        {
            MongoId = id.ToString(),
            Defects = "[]",
            Result = "Aprobado",
            RuleVersion = "V1",
            MaxMinor = 3
        };
        var collection = connection.Collection;
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        try
        {
            await archive.Copy(inspection, default);
            await archive.Copy(inspection, default);
            Assert.Equal(1, await collection.CountDocumentsAsync(filter));
        }
        finally
        {
            await collection.DeleteOneAsync(filter);
        }
    }
}
