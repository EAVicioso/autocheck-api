// AutoCheck academic prototype. No shared database is changed by migration generation.
using Microsoft.EntityFrameworkCore.Design;

namespace AutoCheck.Api.Infrastructure.Data;
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AutoCheckDb>
{
    public AutoCheckDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AutoCheckDb>().UseNpgsql("Host=localhost;Database=autocheck").Options);
}
