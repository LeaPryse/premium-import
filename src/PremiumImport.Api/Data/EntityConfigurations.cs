using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Data;

internal sealed class PremiumMethodConfiguration : IEntityTypeConfiguration<PremiumMethod>
{
    public void Configure(EntityTypeBuilder<PremiumMethod> e)
    {
        e.Property(x => x.Number).HasMaxLength(20).IsRequired();
        e.Property(x => x.Description).HasMaxLength(200).IsRequired();
        e.HasIndex(x => x.Number).IsUnique();
    }
}

internal sealed class MetricConfiguration : IEntityTypeConfiguration<Metric>
{
    public void Configure(EntityTypeBuilder<Metric> e)
    {
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.Description).HasMaxLength(500);
        e.Property(x => x.SourceName).HasMaxLength(100).IsRequired();
        e.HasIndex(x => new { x.PremiumMethodId, x.Name }).IsUnique();
        e.HasOne(x => x.PremiumMethod).WithMany(x => x.Metrics)
            .HasForeignKey(x => x.PremiumMethodId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MetricFieldConfiguration : IEntityTypeConfiguration<MetricField>
{
    public void Configure(EntityTypeBuilder<MetricField> e)
    {
        e.Property(x => x.Key).HasMaxLength(100).IsRequired();
        e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        e.HasIndex(x => new { x.MetricId, x.Key }).IsUnique();
        e.HasOne(x => x.Metric).WithMany(x => x.Fields)
            .HasForeignKey(x => x.MetricId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MetricFieldAliasConfiguration : IEntityTypeConfiguration<MetricFieldAlias>
{
    public void Configure(EntityTypeBuilder<MetricFieldAlias> e)
    {
        e.Property(x => x.Alias).HasMaxLength(100).IsRequired();
        e.HasIndex(x => new { x.MetricFieldId, x.Alias }).IsUnique();
        e.HasOne(x => x.MetricField).WithMany(x => x.Aliases)
            .HasForeignKey(x => x.MetricFieldId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MetricSchemaVersionConfiguration : IEntityTypeConfiguration<MetricSchemaVersion>
{
    public void Configure(EntityTypeBuilder<MetricSchemaVersion> e)
    {
        e.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        e.HasIndex(x => new { x.MetricId, x.Fingerprint }).IsUnique();
        e.HasIndex(x => new { x.MetricId, x.VersionNumber }).IsUnique();
        e.HasOne(x => x.Metric).WithMany(x => x.SchemaVersions)
            .HasForeignKey(x => x.MetricId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MetricSchemaColumnConfiguration : IEntityTypeConfiguration<MetricSchemaColumn>
{
    public void Configure(EntityTypeBuilder<MetricSchemaColumn> e)
    {
        e.Property(x => x.SourceHeader).HasMaxLength(100).IsRequired();
        e.HasIndex(x => new { x.SchemaVersionId, x.Ordinal }).IsUnique();
        e.HasIndex(x => new { x.SchemaVersionId, x.MetricFieldId }).IsUnique();
        e.HasOne(x => x.SchemaVersion).WithMany(x => x.Columns)
            .HasForeignKey(x => x.SchemaVersionId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.MetricField).WithMany(x => x.SchemaColumns)
            .HasForeignKey(x => x.MetricFieldId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> e)
    {
        e.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        e.Property(x => x.ErrorMessage).HasMaxLength(1000);
        // History screen + "latest batch per period" lookup.
        e.HasIndex(x => new { x.MetricId, x.ReportingYear, x.PeriodType, x.PeriodNumber, x.Status });
        e.HasOne(x => x.Metric).WithMany(x => x.ImportBatches)
            .HasForeignKey(x => x.MetricId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.SchemaVersion).WithMany(x => x.ImportBatches)
            .HasForeignKey(x => x.SchemaVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ImportedRowConfiguration : IEntityTypeConfiguration<ImportedRow>
{
    public void Configure(EntityTypeBuilder<ImportedRow> e)
    {
        e.HasIndex(x => new { x.ImportBatchId, x.SourceRowNumber }).IsUnique();
        e.HasOne(x => x.ImportBatch).WithMany(x => x.Rows)
            .HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ImportedValueConfiguration : IEntityTypeConfiguration<ImportedValue>
{
    public void Configure(EntityTypeBuilder<ImportedValue> e)
    {
        // Composite PK: one value per (row, field). Clustered in SQL Server -> a row's values are stored together.
        e.HasKey(x => new { x.ImportedRowId, x.MetricFieldId });
        e.Ignore(x => x.TypedValue);
        e.Property(x => x.TextValue).HasMaxLength(400);
        e.Property(x => x.RawValue).HasMaxLength(400);

        // Typed indexes for filtering/sorting by field: WHERE MetricFieldId = @f AND NumberValue > @x
        e.HasIndex(x => new { x.MetricFieldId, x.TextValue });
        e.HasIndex(x => new { x.MetricFieldId, x.NumberValue });
        e.HasIndex(x => new { x.MetricFieldId, x.DateValue });

        e.HasOne(x => x.ImportedRow).WithMany(x => x.Values)
            .HasForeignKey(x => x.ImportedRowId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.MetricField).WithMany()
            .HasForeignKey(x => x.MetricFieldId).OnDelete(DeleteBehavior.Restrict);
    }
}
