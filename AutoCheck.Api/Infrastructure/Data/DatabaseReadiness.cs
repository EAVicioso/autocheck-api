using Npgsql;

namespace AutoCheck.Api.Infrastructure.Data;

public static class DatabaseReadiness
{
    public static async Task<IResult> Check(AutoCheckDb db, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            if (await db.Database.CanConnectAsync(timeout.Token) && !(await db.Database.GetPendingMigrationsAsync(timeout.Token)).Any())
            {
                return Results.Ok(new { status = "Ready" });
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (NpgsqlException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (TimeoutException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
