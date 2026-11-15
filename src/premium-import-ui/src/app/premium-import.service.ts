import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface PremiumMethod {
  id: number;
  number: string;
  description: string;
  premiumPercent: number;
  calculationPeriod: string;
  metricCount: number;
}

export interface Metric {
  id: number;
  premiumMethodId: number;
  name: string;
  description: string;
  sourceType: string;
  sourceName: string;
  ingestionFrequency: string;
}

export interface MetricField {
  id: number;
  key: string;
  displayName: string;
  dataType: 'text' | 'number' | 'date' | 'boolean';
  isRelevant: boolean;
  isFilterable: boolean;
  isSortable: boolean;
  sourceHeader: string | null;
}

export interface ImportBatch {
  id: number;
  originalFileName: string;
  reportingYear: number;
  reportingPeriod: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  status: string;
  rowCount: number;
  schemaVersion: number;
  errorMessage: string | null;
}

export interface ImportedRow {
  id: number;
  sourceRowNumber: number;
  values: Record<string, string | number | boolean | null>;
}

export interface PagedRows {
  items: ImportedRow[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ImportResult {
  batchId: number;
  rowCount: number;
  schemaVersion: number;
  newSchema: boolean;
}

@Injectable({ providedIn: 'root' })
export class PremiumImportService {
  private readonly baseUrl = 'http://localhost:5000/api';

  constructor(private readonly http: HttpClient) {}

  getPremiumMethods(): Observable<PremiumMethod[]> {
    return this.http.get<PremiumMethod[]>(`${this.baseUrl}/premium-methods`);
  }

  getMetrics(premiumMethodId: number): Observable<Metric[]> {
    return this.http.get<Metric[]>(`${this.baseUrl}/premium-methods/${premiumMethodId}/metrics`);
  }

  getFields(metricId: number): Observable<MetricField[]> {
    return this.http.get<MetricField[]>(`${this.baseUrl}/metrics/${metricId}/fields`);
  }

  getImports(metricId: number): Observable<ImportBatch[]> {
    return this.http.get<ImportBatch[]>(`${this.baseUrl}/metrics/${metricId}/imports`);
  }

  getRows(
    metricId: number,
    page: number,
    pageSize: number,
    filters: Record<string, string>,
    sortBy: string,
    descending: boolean,
    batchId: number | null
  ): Observable<PagedRows> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('descending', descending);
    if (sortBy) {
      params = params.set('sortBy', sortBy);
    }
    if (batchId !== null) {
      params = params.set('batchId', batchId);
    }
    for (const [key, value] of Object.entries(filters)) {
      if (value.trim()) {
        params = params.set(`filters[${key}]`, value.trim());
      }
    }
    return this.http.get<PagedRows>(`${this.baseUrl}/metrics/${metricId}/rows`, { params });
  }

  upload(
    metricId: number,
    file: File,
    reportingYear: number,
    reportingPeriod: string
  ): Observable<ImportResult> {
    const body = new FormData();
    body.append('file', file);
    body.append('reportingYear', reportingYear.toString());
    body.append('reportingPeriod', reportingPeriod);
    return this.http.post<ImportResult>(`${this.baseUrl}/metrics/${metricId}/imports`, body);
  }
}
