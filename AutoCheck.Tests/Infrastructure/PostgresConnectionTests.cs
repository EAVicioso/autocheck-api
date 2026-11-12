using AutoCheck.Api.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AutoCheck.Tests.Infrastructure;

public class PostgresConnectionTests
{
    private static IConfiguration Config(string? runtime, string? migrations = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:Postgres"] = runtime, ["ConnectionStrings:PostgresMigrations"] = migrations }).Build();

    [Fact] public void Missing_configuration_fails_without_an_implicit_database()
    { Assert.Throws<InvalidOperationException>(() => PostgresConnection.Resolve(Config(null))); }

    [Theory]
    [InlineData("Host=remote.example;Database=postgres;Username=api;Password=private-marker;SSL Mode=Disable")]
    [InlineData("Host=remote.example;Database=postgres;Username=api;Password=private-marker;SSL Mode=Require")]
    [InlineData("Host=127.0.0.1;Database=postgres;Username=api;Password=private-marker;Maximum Pool Size=100")]
    [InlineData("Host=127.0.0.1;Database=postgres;Username=api;Password=private-marker;Command Timeout=0")]
    [InlineData("Password=private-marker;invalid-option=bad")]
    public void Unsafe_settings_are_rejected_without_disclosing_secrets(string raw)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PostgresConnection.Resolve(Config(raw)));
        Assert.DoesNotContain("private-marker", error.ToString());
    }

    [Fact] public void Migrations_use_a_separate_identity_and_runtime_has_bounded_pooling()
    {
        var config = Config("Host=remote.example;Database=postgres;Username=api;SSL Mode=VerifyFull",
            "Host=remote.example;Database=postgres;Username=migrator;SSL Mode=VerifyFull;Maximum Pool Size=2");
        var runtime = new NpgsqlConnectionStringBuilder(PostgresConnection.Resolve(config));
        var migrations = new NpgsqlConnectionStringBuilder(PostgresConnection.Resolve(config, migrations: true));
        Assert.Equal("api", runtime.Username); Assert.Equal(10, runtime.MaxPoolSize);
        Assert.Equal(10, runtime.Timeout); Assert.Equal(30, runtime.CommandTimeout); Assert.Equal(30, runtime.KeepAlive);
        Assert.Equal("migrator", migrations.Username); Assert.Equal(2, migrations.MaxPoolSize);
        Assert.False(runtime.IncludeErrorDetail); Assert.False(runtime.LogParameters);
    }
}
