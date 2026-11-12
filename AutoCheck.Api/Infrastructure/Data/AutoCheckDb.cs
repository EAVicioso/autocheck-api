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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("autocheck");
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.AuthId).IsUnique();
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<UserAccount>().Property(x => x.Email).HasMaxLength(254);
        modelBuilder.Entity<UserAccount>().HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AccountLock>().HasKey(x => x.Email);
        modelBuilder.Entity<AppSession>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.Vin).IsUnique();
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.Plate).IsUnique();
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.OwnerId);
        modelBuilder.Entity<Vehicle>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Vehicle>().HasOne<CatalogItem>().WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Vehicle>().HasOne<CatalogItem>().WithMany().HasForeignKey(x => x.ColorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Ramp>().HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CenterSchedule>().HasKey(x => new { x.CenterId, x.Day });
        modelBuilder.Entity<CenterSchedule>().HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BookingBlock>().HasKey(x => new { x.RampId, x.Starts });
        modelBuilder.Entity<BookingBlock>().HasIndex(x => new { x.State, x.Expires });
        modelBuilder.Entity<BookingBlock>().HasOne<Ramp>().WithMany().HasForeignKey(x => x.RampId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentAttempt>().HasIndex(x => x.UserId);
        modelBuilder.Entity<PaymentAttempt>().Property(x => x.Amount).HasPrecision(12, 2).HasDefaultValue(1000m);
        modelBuilder.Entity<PaymentAttempt>().Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("DOP");
        modelBuilder.Entity<PaymentAttempt>().HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentEvent>().HasKey(x => x.Id);
        modelBuilder.Entity<PaymentEvent>().HasOne<PaymentAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Appointment>().HasIndex(x => x.AttemptId).IsUnique();
        modelBuilder.Entity<Appointment>().HasIndex(x => new { x.CenterId, x.Starts });
        modelBuilder.Entity<Appointment>().HasOne<PaymentAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Appointment>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.InspectorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionRule>().HasIndex(x => x.Version).IsUnique();
        modelBuilder.Entity<InspectionRule>().HasIndex(x => x.Effective).IsUnique();
        modelBuilder.Entity<Inspection>().HasIndex(x => x.AppointmentId).IsUnique();
        modelBuilder.Entity<Inspection>().HasIndex(x => new { x.VehicleId, x.Closed });
        modelBuilder.Entity<Inspection>().HasIndex(x => new { x.InspectorId, x.CenterId, x.Closed, x.Id });
        modelBuilder.Entity<Inspection>().HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Inspection>().HasOne<Inspection>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Inspection>().Property(x => x.Defects).HasColumnType("jsonb");
        modelBuilder.Entity<Inspection>().Property(x => x.MongoId).HasMaxLength(24);
        modelBuilder.Entity<Inspection>().Property(x => x.Revision).IsConcurrencyToken();
        modelBuilder.Entity<Certificate>().HasIndex(x => x.InspectionId).IsUnique();
        modelBuilder.Entity<Certificate>().HasOne<Inspection>().WithMany().HasForeignKey(x => x.InspectionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Certificate>().Property(x => x.ManifestHash).HasMaxLength(64);
        modelBuilder.Entity<Certificate>().Property(x => x.PdfHash).HasMaxLength(64);
        modelBuilder.Entity<AuditEvent>().HasIndex(x => x.At);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.Kind, x.AggregateId }).IsUnique();
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.Completed, x.Available });
        modelBuilder.Entity<IdempotencyRecord>().HasKey(x => x.Key);
        modelBuilder.Entity<DailyMetric>().HasKey(x => new { x.Day, x.CenterId, x.RampId, x.InspectorId });
        modelBuilder.Entity<AnomalyAlert>().HasIndex(x => new { x.InspectorId, x.CenterId, x.FinalInspectionId }).IsUnique();
        modelBuilder.Entity<InspectionRule>().HasData(new InspectionRule { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Version = "V1", MaxMinor = 3, Effective = DateTimeOffset.UnixEpoch });
        modelBuilder.Entity<CatalogItem>().HasData(new CatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Type = "marcas", Name = "Toyota" }, new CatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Type = "modelos", Name = "Corolla", ParentId = Guid.Parse("20000000-0000-0000-0000-000000000001") }, new CatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Type = "colores", Name = "Blanco" }, new CatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Type = "categorias", Name = "Liviano" });
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.ClrType.Name);
        }
    }
}
