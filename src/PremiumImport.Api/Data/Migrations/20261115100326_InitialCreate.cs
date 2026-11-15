using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremiumImport.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PremiumMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PremiumPercent = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CalculationPeriod = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Metrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PremiumMethodId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IngestionFrequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Metrics_PremiumMethods_PremiumMethodId",
                        column: x => x.PremiumMethodId,
                        principalTable: "PremiumMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetricFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MetricId = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsRelevant = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFilterable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSortable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricFields_Metrics_MetricId",
                        column: x => x.MetricId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetricSchemaVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MetricId = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricSchemaVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricSchemaVersions_Metrics_MetricId",
                        column: x => x.MetricId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetricFieldAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MetricFieldId = table.Column<int>(type: "INTEGER", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricFieldAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricFieldAliases_MetricFields_MetricFieldId",
                        column: x => x.MetricFieldId,
                        principalTable: "MetricFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MetricId = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ReportingYear = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PeriodNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_MetricSchemaVersions_SchemaVersionId",
                        column: x => x.SchemaVersionId,
                        principalTable: "MetricSchemaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportBatches_Metrics_MetricId",
                        column: x => x.MetricId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetricSchemaColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SchemaVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MetricFieldId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceHeader = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricSchemaColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricSchemaColumns_MetricFields_MetricFieldId",
                        column: x => x.MetricFieldId,
                        principalTable: "MetricFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MetricSchemaColumns_MetricSchemaVersions_SchemaVersionId",
                        column: x => x.SchemaVersionId,
                        principalTable: "MetricSchemaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportedRows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportBatchId = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceRowNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportedRows_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportedValues",
                columns: table => new
                {
                    ImportedRowId = table.Column<long>(type: "INTEGER", nullable: false),
                    MetricFieldId = table.Column<int>(type: "INTEGER", nullable: false),
                    TextValue = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    NumberValue = table.Column<double>(type: "REAL", precision: 18, scale: 4, nullable: true),
                    DateValue = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BooleanValue = table.Column<bool>(type: "INTEGER", nullable: true),
                    RawValue = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedValues", x => new { x.ImportedRowId, x.MetricFieldId });
                    table.ForeignKey(
                        name: "FK_ImportedValues_ImportedRows_ImportedRowId",
                        column: x => x.ImportedRowId,
                        principalTable: "ImportedRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportedValues_MetricFields_MetricFieldId",
                        column: x => x.MetricFieldId,
                        principalTable: "MetricFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_MetricId_ReportingYear_PeriodType_PeriodNumber_Status",
                table: "ImportBatches",
                columns: new[] { "MetricId", "ReportingYear", "PeriodType", "PeriodNumber", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_SchemaVersionId",
                table: "ImportBatches",
                column: "SchemaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportedRows_ImportBatchId_SourceRowNumber",
                table: "ImportedRows",
                columns: new[] { "ImportBatchId", "SourceRowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedValues_MetricFieldId_DateValue",
                table: "ImportedValues",
                columns: new[] { "MetricFieldId", "DateValue" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedValues_MetricFieldId_NumberValue",
                table: "ImportedValues",
                columns: new[] { "MetricFieldId", "NumberValue" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedValues_MetricFieldId_TextValue",
                table: "ImportedValues",
                columns: new[] { "MetricFieldId", "TextValue" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricFieldAliases_MetricFieldId_Alias",
                table: "MetricFieldAliases",
                columns: new[] { "MetricFieldId", "Alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetricFields_MetricId_Key",
                table: "MetricFields",
                columns: new[] { "MetricId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metrics_PremiumMethodId_Name",
                table: "Metrics",
                columns: new[] { "PremiumMethodId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetricSchemaColumns_MetricFieldId",
                table: "MetricSchemaColumns",
                column: "MetricFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_MetricSchemaColumns_SchemaVersionId_MetricFieldId",
                table: "MetricSchemaColumns",
                columns: new[] { "SchemaVersionId", "MetricFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetricSchemaColumns_SchemaVersionId_Ordinal",
                table: "MetricSchemaColumns",
                columns: new[] { "SchemaVersionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetricSchemaVersions_MetricId_Fingerprint",
                table: "MetricSchemaVersions",
                columns: new[] { "MetricId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetricSchemaVersions_MetricId_VersionNumber",
                table: "MetricSchemaVersions",
                columns: new[] { "MetricId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiumMethods_Number",
                table: "PremiumMethods",
                column: "Number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportedValues");

            migrationBuilder.DropTable(
                name: "MetricFieldAliases");

            migrationBuilder.DropTable(
                name: "MetricSchemaColumns");

            migrationBuilder.DropTable(
                name: "ImportedRows");

            migrationBuilder.DropTable(
                name: "MetricFields");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "MetricSchemaVersions");

            migrationBuilder.DropTable(
                name: "Metrics");

            migrationBuilder.DropTable(
                name: "PremiumMethods");
        }
    }
}
