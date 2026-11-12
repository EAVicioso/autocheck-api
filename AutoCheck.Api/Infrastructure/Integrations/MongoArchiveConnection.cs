using MongoDB.Bson;
using MongoDB.Driver;

namespace AutoCheck.Api.Infrastructure;
// One pool per process; optional projection configuration is resolved on first delivery.
public sealed class MongoArchiveConnection(IConfiguration config) : IDisposable
{
    private readonly Lazy<MongoClient> client = new(() => CreateClient(config));
    public IMongoCollection<BsonDocument> Collection => client.Value.GetDatabase("autocheck_inspections_db").GetCollection<BsonDocument>("reportes_defectos");

    private static MongoClient CreateClient(IConfiguration config)
    {
        var connection = config["Mongo:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw ApiContract.Unavailable();
        }

        var settings = MongoClientSettings.FromConnectionString(connection);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
        settings.ConnectTimeout = TimeSpan.FromSeconds(2);
        settings.SocketTimeout = TimeSpan.FromSeconds(3);
        settings.MaxConnectionPoolSize = 20;
        return new MongoClient(settings);
    }

    public void Dispose()
    {
        if (client.IsValueCreated)
        {
            client.Value.Dispose();
        }
    }
}
