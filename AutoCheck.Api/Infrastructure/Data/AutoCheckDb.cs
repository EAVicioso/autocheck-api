// AutoCheck academic prototype, SRS 1.1; RD-04, RF-INS-01, RS-03.
namespace AutoCheck.Api.Infrastructure.Data;

public sealed class AutoCheckDb(DbContextOptions<AutoCheckDb> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<AccountLock> AccountLocks => Set<AccountLock>();
    public DbSet<AppSession> Sessions => Set<AppSession>();
    public DbSet<CatalogItem> Catalog => Set<CatalogItem>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Center> Centers => Set<Center>();
    public DbSet<Ramp> Ramps => Set<Ramp>();
    public DbSet<CenterSchedule> Schedules => Set<CenterSchedule>();
    public DbSet<BookingBlock> Blocks => Set<BookingBlock>();
    public DbSet<PaymentAttempt> Payments => Set<PaymentAttempt>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<InspectionRule> Rules => Set<InspectionRule>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();
    public DbSet<DailyMetric> Metrics => Set<DailyMetric>();
    public DbSet<AnomalyAlert> Alerts => Set<AnomalyAlert>();
    public DbSet<AggregationState> Aggregation => Set<AggregationState>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema("autocheck");
        m.Entity<UserAccount>().HasIndex(x => x.AuthId).IsUnique();
        m.Entity<UserAccount>().HasIndex(x => x.Email).IsUnique();
        m.Entity<UserAccount>().Property(x => x.Email).HasMaxLength(254);
        m.Entity<UserAccount>().HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AccountLock>().HasKey(x => x.Email);
        m.Entity<AppSession>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Vehicle>().HasIndex(x => x.Vin).IsUnique();
        m.Entity<Vehicle>().HasIndex(x => x.Plate).IsUnique();
        m.Entity<Vehicle>().HasIndex(x => x.OwnerId);
        m.Entity<Vehicle>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Vehicle>().HasOne<CatalogItem>().WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Vehicle>().HasOne<CatalogItem>().WithMany().HasForeignKey(x => x.ColorId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Ramp>().HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CenterSchedule>().HasKey(x => new { x.CenterId, x.Day });
        m.Entity<CenterSchedule>().HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<BookingBlock>().HasKey(x => new { x.RampId, x.Starts });
        m.Entity<BookingBlock>().HasOne<Ramp>().WithMany().HasForeignKey(x => x.RampId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PaymentAttempt>().HasIndex(x => x.UserId);
        m.Entity<PaymentAttempt>().HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PaymentEvent>().HasKey(x => x.Id);
        m.Entity<PaymentEvent>().HasOne<PaymentAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Appointment>().HasIndex(x => x.AttemptId).IsUnique();
        m.Entity<Appointment>().HasIndex(x => new { x.CenterId, x.Starts });
        m.Entity<Appointment>().HasOne<PaymentAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Appointment>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.InspectorId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<InspectionRule>().HasIndex(x => x.Version).IsUnique();
        m.Entity<InspectionRule>().HasIndex(x => x.Effective).IsUnique();
        m.Entity<Inspection>().HasIndex(x => x.AppointmentId).IsUnique();
        m.Entity<Inspection>().HasIndex(x => new { x.VehicleId, x.Closed });
        m.Entity<Inspection>().HasIndex(x => new { x.InspectorId, x.CenterId, x.Closed, x.Id });
        m.Entity<Inspection>().HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Inspection>().HasOne<Inspection>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Inspection>().Property(x => x.Defects).HasColumnType("jsonb");
        m.Entity<Inspection>().Property(x => x.MongoId).HasMaxLength(24);
        m.Entity<Inspection>().Property(x => x.Revision).IsConcurrencyToken();
        m.Entity<Certificate>().HasIndex(x => x.InspectionId).IsUnique();
        m.Entity<Certificate>().HasOne<Inspection>().WithMany().HasForeignKey(x => x.InspectionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Certificate>().Property(x => x.ManifestHash).HasMaxLength(64);
        m.Entity<Certificate>().Property(x => x.PdfHash).HasMaxLength(64);
        m.Entity<AuditEvent>().HasIndex(x => x.At);
        m.Entity<OutboxMessage>().HasIndex(x => new { x.Kind, x.AggregateId }).IsUnique();
        m.Entity<OutboxMessage>().HasIndex(x => new { x.Completed, x.Available });
        m.Entity<IdempotencyRecord>().HasKey(x => x.Key);
        m.Entity<DailyMetric>().HasKey(x => new { x.Day, x.CenterId, x.RampId, x.InspectorId });
        m.Entity<AnomalyAlert>().HasIndex(x => new { x.InspectorId, x.CenterId, x.FinalInspectionId }).IsUnique();
        m.Entity<InspectionRule>().HasData(new InspectionRule { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Version = "V1", MaxMinor = 3, Effective = DateTimeOffset.UnixEpoch });
        m.Entity<CatalogItem>().HasData(
            new CatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Type = "marcas", Name = "Toyota" },
            new CatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Type = "modelos", Name = "Corolla", ParentId = Guid.Parse("20000000-0000-0000-0000-000000000001") },
            new CatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Type = "colores", Name = "Blanco" },
            new CatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Type = "categorias", Name = "Liviano" });
        foreach (var entity in m.Model.GetEntityTypes()) entity.SetTableName(entity.ClrType.Name);
    }
}
