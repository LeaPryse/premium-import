// Mirrors the API contracts. Enum values are camelCase strings (JsonStringEnumConverter on the server).

export type DataType = 'text' | 'number' | 'date' | 'boolean';
export type PeriodType = 'month' | 'quarter' | 'halfYear' | 'year';
export type SourceType = 'excel' | 'externalApi';
export type Frequency = 'daily' | 'weekly' | 'monthly' | 'quarterly' | 'yearly';
export type ImportStatus = 'processing' | 'completed' | 'failed';

export interface PremiumMethod {
  id: number;
  number: string;
  description: string;
  premiumPercent: number;
  calculationPeriod: PeriodType;
  metricCount: number;
}

export interface Metric {
  id: number;
  premiumMethodId: number;
  name: string;
  description: string;
  sourceType: SourceType;
  sourceName: string;
  ingestionFrequency: Frequency;
  fieldCount: number;
  lastImportAtUtc: string | null;
}

export interface MetricField {
  id: number;
  key: string;
  displayName: string;
  dataType: DataType;
  isRelevant: boolean;
  isFilterable: boolean;
  isSortable: boolean;
  sourceHeader: string | null;
  aliases: string[];
}

export interface ImportBatch {
  id: number;
  originalFileName: string;
  reportingYear: number;
  periodType: PeriodType;
  periodNumber: number;
  startedAtUtc: string;
  completedAtUtc: string | null;
  status: ImportStatus;
  rowCount: number;
  schemaVersion: number;
  errorMessage: string | null;
  isSuperseded: boolean;
}

export type CellValue = string | number | boolean | null;

export interface ImportedRow {
  id: number;
  batchId: number;
  sourceRowNumber: number;
  values: Record<string, CellValue>;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ImportResult {
  batchId: number;
  rowCount: number;
  schemaVersion: number;
  newSchema: boolean;
  newFields: string[];
}

export interface ValueCount {
  value: CellValue;
  count: number;
}

export interface FieldFilterOptions {
  fieldKey: string;
  dataType: DataType;
  operators: string[];
  values: ValueCount[] | null;
  min: string | number | null;
  max: string | number | null;
}

export interface RowsQuery {
  /** null = the latest completed batch of every reporting period */
  batchId: number | null;
  page: number;
  pageSize: number;
  sortBy: string;
  descending: boolean;
  /** fieldKey -> filter expression (see FilterParser on the server) */
  filters: Record<string, string>;
}

export interface ImportRequest {
  file: File;
  reportingYear: number;
  periodType: PeriodType;
  periodNumber: number;
}
