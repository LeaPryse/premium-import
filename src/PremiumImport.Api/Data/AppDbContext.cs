using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PremiumMethod> PremiumMethods => Set<PremiumMethod>();
    public DbSet<Metric> Metrics => Set<Metric>();
    public DbSet<MetricField> MetricFields => Set<MetricField>();
    public DbSet<MetricFieldAlias> MetricFieldAliases => Set<MetricFieldAlias>();
    public DbSet<MetricSchemaVersion> MetricSchemaVersions => Set<MetricSchemaVersion>();
    public DbSet<MetricSchemaColumn> MetricSchemaColumns => Set<MetricSchemaColumn>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportedRow> ImportedRows => Set<ImportedRow>();
    public DbSet<ImportedValue> ImportedValues => Set<ImportedValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // SQLite has no DECIMAL type (EF stores it as TEXT, which sorts/compares as text).
        // For SQLite only, store numeric values as REAL so range filters, ORDER BY and MIN/MAX work.
        // On SQL Server the column stays DECIMAL(18,4).
        if (Database.IsSqlite())
            modelBuilder.Entity<ImportedValue>().Property(x => x.NumberValue).HasConversion<double?>();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums are stored as readable strings (e.g. 'Number', 'Quarter') - easier to query in plain SQL.
        builder.Properties<DataType>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<SourceType>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<PeriodType>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<Frequency>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<ImportStatus>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<decimal>().HavePrecision(18, 4);
    }
}
