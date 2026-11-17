// AutoCheck / INTEC, SRS 1.1; CU-05 / PA-05. Version 1.0; history: Git.
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

namespace AutoCheck.Api.Infrastructure.Data;

public sealed class AutoCheckReportsDbContext(DbContextOptions<AutoCheckReportsDbContext> options, IMongoClient client) : DbContext(options)
{
    public const string DatabaseName = "AutoCheckReports";
    public const string CollectionName = "Reports";
    public DbSet<InspectionReport> Reports => Set<InspectionReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InspectionReport>().ToCollection(CollectionName);
        modelBuilder.Entity<InspectionReport>().OwnsMany(x => x.Defects);
    }

    // One atomic replacement, including creation. EF read/then Add would race on redelivery.
    public Task Upsert(InspectionReport report, CancellationToken ct) => client
        .GetDatabase(DatabaseName).GetCollection<InspectionReport>(CollectionName)
        .ReplaceOneAsync(x => x.Id == report.Id, report, new ReplaceOptions { IsUpsert = true }, ct);
}

public sealed class InspectionReport
{
    public ObjectId Id { get; set; }
    [BsonElement("inspeccionId")]
    public string InspectionId { get; set; } = "";
    [BsonElement("defectos")]
    public List<ReportDefect> Defects { get; set; } = [];
    [BsonElement("resultado")]
    public string Result { get; set; } = "";
    [BsonElement("L")]
    public int Minor { get; set; }
    [BsonElement("Md")]
    public int Medium { get; set; }
    [BsonElement("G")]
    public int Serious { get; set; }
    [BsonElement("M")]
    public int MaxMinor { get; set; }
    [BsonElement("versionReglas")]
    public string RuleVersion { get; set; } = "";
}

public sealed class ReportDefect
{
    [BsonElement("categoria")]
    public string? Categoria { get; set; }
    [BsonElement("subcategoria")]
    public string? Subcategoria { get; set; }
    [BsonElement("descripcion")]
    public string Descripcion { get; set; } = "";
    [BsonElement("severidad")]
    public string Severidad { get; set; } = "";
    [BsonElement("cantidad")]
    public int Cantidad { get; set; }
}
