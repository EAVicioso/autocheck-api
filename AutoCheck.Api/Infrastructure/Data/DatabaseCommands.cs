namespace AutoCheck.Api.Infrastructure.Data;

// Maintenance commands exit before starting HTTP listeners or background consumers.
public static class DatabaseCommands
{
    public static bool IsCommand(string argument) => argument is "--migrate" or "--bootstrap-admin" or "--check-database";

    public static async Task Run(WebApplication app, string[] arguments)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoCheckDb>();
        if (arguments.Contains("--migrate"))
        {
            await db.Database.MigrateAsync();
        }
        if (arguments.Contains("--check-database"))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await DatabaseConnectivity.Verify(db, timeout.Token);
            Console.WriteLine(JsonSerializer.Serialize(result));
        }
        if (arguments.Contains("--bootstrap-admin"))
        {
            await Bootstrap(db, app.Configuration);
        }
    }

    private static async Task Bootstrap(AutoCheckDb db, IConfiguration configuration)
    {
        var id = Guid.Parse(configuration["Bootstrap:AuthId"] ?? throw new InvalidOperationException("Bootstrap:AuthId is required."));
        var email = ApiContract.NormalizeEmail(configuration["Bootstrap:Email"] ?? throw new InvalidOperationException("Bootstrap:Email is required."));
        var user = await db.Users.SingleOrDefaultAsync(account => account.AuthId == id);
        if (user is null)
        {
            user = new UserAccount { AuthId = id, Email = email, Name = "Administrador" };
            db.Users.Add(user);
        }
        user.Role = Roles.Admin;
        user.CenterId = null;
        user.Permissions = Permissions.All.ToArray();
        db.Audit.Add(new AuditEvent { At = DateTimeOffset.UtcNow, Operation = "BOOTSTRAP_ADMIN", Entity = user.Id.ToString(), Outcome = "CONFIGURADO", Correlation = Guid.NewGuid().ToString() });
        await db.SaveChangesAsync();
    }
}
