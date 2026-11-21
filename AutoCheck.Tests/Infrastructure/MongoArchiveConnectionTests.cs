using AutoCheck.Api.Infrastructure;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AutoCheck.Tests.Infrastructure;

public sealed class MongoArchiveConnectionTests
{
    [Theory]
    [InlineData("mongodb://mongo.example.test")]
    [InlineData("mongodb://127.0.0.1,localhost")]
    [InlineData("mongodb://127.0.0.1/?w=0")]
    [InlineData("mongodb://mongo.example.test/?tls=true&tlsInsecure=true")]
    public void RejectsInsecureOrUnacknowledgedSettings(string uri)
    {
        using var connection = Connect(uri);
        Assert.Equal(503, Assert.Throws<ApiException>(() => connection.Client).Status);
    }

    [Fact]
    public void ForcesDirectLoopbackAndMajorityAcknowledgment()
    {
        using var connection = Connect("mongodb://127.0.0.1/?directConnection=false&w=1");
        var settings = connection.Client.Settings;
        Assert.False(settings.UseTls);
        Assert.True(settings.DirectConnection);
        Assert.Equal(WriteConcern.WMajority, settings.WriteConcern);
    }

    private static MongoArchiveConnection Connect(string uri) => new(Options.Create(new MongoDbOptions { ConnectionString = uri }));
}
