using Npgsql;

namespace AutoCheck.Api.Infrastructure.Data;
// Shared by the server and EF tooling. Configuration errors never include credentials.
public static class PostgresConnection
{
    public static string Resolve(IConfiguration configuration, bool migrations = false)
    {
        var raw = (migrations ? configuration.GetConnectionString("PostgresMigrations") : null) ?? configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Configure ConnectionStrings:Postgres using user-secrets or environment variables.");
        }

        NpgsqlConnectionStringBuilder connection;
        try
        {
            connection = new(raw);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The PostgreSQL connection configuration is invalid.");
        }

        if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Database) || string.IsNullOrWhiteSpace(connection.Username))
        {
            throw new InvalidOperationException("PostgreSQL requires Host, Database and Username.");
        }

        var local = connection.Host is "localhost" or "127.0.0.1" or "::1";
        if (!local && connection.SslMode != SslMode.VerifyFull)
        {
            throw new InvalidOperationException("Remote PostgreSQL requires SSL Mode=VerifyFull and certificate validation.");
        }

        ApplyDefaults(connection, migrations);

        if (connection.MaxPoolSize < 1 || connection.MaxPoolSize > 20 || connection.Timeout is < 1 or > 30 || connection.CommandTimeout is < 1 or > 120)
        {
            throw new InvalidOperationException("PostgreSQL requires a pool of 1–20 connections and bounded timeouts (connect 1–30s, command 1–120s).");
        }

        connection.IncludeErrorDetail = false;
        connection.LogParameters = false;
        connection.PersistSecurityInfo = false;
        // Do not enable automatic EF transaction replay: Mutation owns transactions and external effects.
        return connection.ConnectionString;
    }

    private static void ApplyDefaults(NpgsqlConnectionStringBuilder connection, bool migrations)
    {
        if (!connection.ShouldSerialize("Maximum Pool Size"))
        {
            connection.MaxPoolSize = 10;
        }

        if (!connection.ShouldSerialize("Timeout"))
        {
            connection.Timeout = 10;
        }

        if (!connection.ShouldSerialize("Command Timeout"))
        {
            connection.CommandTimeout = 30;
        }

        if (!connection.ShouldSerialize("Keepalive"))
        {
            connection.KeepAlive = 30;
        }

        if (!connection.ShouldSerialize("Connection Idle Lifetime"))
        {
            connection.ConnectionIdleLifetime = 60;
        }

        if (!connection.ShouldSerialize("Application Name"))
        {
            connection.ApplicationName = migrations ? "AutoCheck.Migrations" : "AutoCheck.Api";
        }

    }
}
