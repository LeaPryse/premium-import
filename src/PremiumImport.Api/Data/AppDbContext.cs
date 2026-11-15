using Microsoft.EntityFrameworkCore;

namespace PremiumImport.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PremiumMethod> PremiumMethods => Set<PremiumMethod>();
    public DbSet<Metric> Metrics => Set<Metric>();
    public DbSet<MetricField> MetricFields => Set<MetricField>();
    public DbSet<MetricSchemaVersion> MetricSchemaVersions => Set<MetricSchemaVersion>();
    public DbSet<MetricSchemaColumn> MetricSchemaColumns => Set<MetricSchemaColumn>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportedRow> ImportedRows => Set<ImportedRow>();
    public DbSet<ImportedValue> ImportedValues => Set<ImportedValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PremiumMethod>()
            .HasIndex(x => x.Number).IsUnique();

        modelBuilder.Entity<Metric>()
            .HasIndex(x => new { x.PremiumMethodId, x.Name }).IsUnique();

        modelBuilder.Entity<MetricField>()
            .HasIndex(x => new { x.MetricId, x.Key }).IsUnique();
        modelBuilder.Entity<MetricField>()
            .HasIndex(x => new { x.MetricId, x.IsRelevant });

        modelBuilder.Entity<MetricSchemaVersion>()
            .HasIndex(x => new { x.MetricId, x.Fingerprint }).IsUnique();
        modelBuilder.Entity<MetricSchemaVersion>()
            .HasIndex(x => new { x.MetricId, x.VersionNumber }).IsUnique();

        modelBuilder.Entity<MetricSchemaColumn>()
            .HasIndex(x => new { x.SchemaVersionId, x.Ordinal }).IsUnique();
        modelBuilder.Entity<MetricSchemaColumn>()
            .HasIndex(x => new { x.SchemaVersionId, x.MetricFieldId }).IsUnique();

        modelBuilder.Entity<ImportBatch>()
            .HasIndex(x => new { x.MetricId, x.ReportingYear, x.ReportingPeriod, x.StartedAtUtc });
        modelBuilder.Entity<ImportBatch>()
            .HasIndex(x => new { x.MetricId, x.Status });

        modelBuilder.Entity<ImportedRow>()
            .HasIndex(x => new { x.ImportBatchId, x.SourceRowNumber }).IsUnique();

        modelBuilder.Entity<ImportedValue>()
            .HasIndex(x => new { x.ImportedRowId, x.MetricFieldId }).IsUnique();
        modelBuilder.Entity<ImportedValue>()
            .HasIndex(x => new { x.MetricFieldId, x.TextValue });
        modelBuilder.Entity<ImportedValue>()
            .HasIndex(x => new { x.MetricFieldId, x.NumberValue });
        modelBuilder.Entity<ImportedValue>()
            .HasIndex(x => new { x.MetricFieldId, x.DateValue });

        modelBuilder.Entity<Metric>()
            .HasOne(x => x.PremiumMethod).WithMany(x => x.Metrics)
            .HasForeignKey(x => x.PremiumMethodId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MetricSchemaColumn>()
            .HasOne(x => x.MetricField).WithMany(x => x.SchemaColumns)
            .HasForeignKey(x => x.MetricFieldId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ImportBatch>()
            .HasOne(x => x.SchemaVersion).WithMany(x => x.ImportBatches)
            .HasForeignKey(x => x.SchemaVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
