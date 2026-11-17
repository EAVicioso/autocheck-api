using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Options;

namespace AutoCheck.Api.Infrastructure;
// One pool per process; optional projection configuration is resolved on first delivery.
public sealed class MongoArchiveConnection(IOptions<MongoDbOptions> options) : IDisposable
{
    private readonly Lazy<IMongoClient> client = new(() => CreateClient(options.Value));
    public IMongoClient Client => client.Value;
    public IMongoCollection<BsonDocument> Collection => Client.GetDatabase(AutoCheckReportsDbContext.DatabaseName).GetCollection<BsonDocument>(AutoCheckReportsDbContext.CollectionName);
    public AutoCheckReportsDbContext CreateContext() => new(new DbContextOptionsBuilder<AutoCheckReportsDbContext>().UseMongoDB(Client, AutoCheckReportsDbContext.DatabaseName).Options, Client);

    private static MongoClient CreateClient(MongoDbOptions options)
    {
        var connection = options.ConnectionString;
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

public sealed class MongoDbOptions
{
    public string? ConnectionString { get; set; }
}
