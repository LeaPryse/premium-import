import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  FieldFilterOptions,
  ImportBatch,
  ImportedRow,
  ImportRequest,
  ImportResult,
  Metric,
  MetricField,
  PagedResult,
  PremiumMethod,
  RowsQuery
} from './models';

export const API_BASE_URL = 'http://localhost:5000/api';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  getPremiumMethods(): Observable<PremiumMethod[]> {
    return this.http.get<PremiumMethod[]>(`${API_BASE_URL}/premium-methods`);
  }

  getMetrics(premiumMethodId: number): Observable<Metric[]> {
    return this.http.get<Metric[]>(`${API_BASE_URL}/premium-methods/${premiumMethodId}/metrics`);
  }

  getFields(metricId: number): Observable<MetricField[]> {
    return this.http.get<MetricField[]>(`${API_BASE_URL}/metrics/${metricId}/fields`);
  }

  getFilterOptions(metricId: number, fieldId: number): Observable<FieldFilterOptions> {
    return this.http.get<FieldFilterOptions>(`${API_BASE_URL}/metrics/${metricId}/fields/${fieldId}/filter-options`);
  }

  getImports(metricId: number): Observable<ImportBatch[]> {
    return this.http.get<ImportBatch[]>(`${API_BASE_URL}/metrics/${metricId}/imports`);
  }

  getRows(metricId: number, query: RowsQuery): Observable<PagedResult<ImportedRow>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('descending', query.descending);
    if (query.sortBy) {
      params = params.set('sortBy', query.sortBy);
    }
    if (query.batchId !== null) {
      params = params.set('batchId', query.batchId);
    }
    for (const [key, expression] of Object.entries(query.filters)) {
      params = params.set(`filters[${key}]`, expression);
    }
    return this.http.get<PagedResult<ImportedRow>>(`${API_BASE_URL}/metrics/${metricId}/rows`, { params });
  }

  upload(metricId: number, request: ImportRequest): Observable<ImportResult> {
    const body = new FormData();
    body.append('file', request.file);
    body.append('reportingYear', String(request.reportingYear));
    body.append('periodType', request.periodType);
    body.append('periodNumber', String(request.periodNumber));
    return this.http.post<ImportResult>(`${API_BASE_URL}/metrics/${metricId}/imports`, body);
  }
}

/** Extracts a readable message from an API ProblemDetails / validation response. */
export function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'לא ניתן להתחבר לשרת. ודאו שה־API פועל ונסו שוב.';
    }
    const body = error.error;
    const validation = body?.errors && Object.values(body.errors as Record<string, string[]>)[0]?.[0];
    if (validation) {
      return validation;
    }
    if (typeof body?.title === 'string') {
      return body.title;
    }
  }
  return 'אירעה שגיאה בלתי צפויה.';
}
