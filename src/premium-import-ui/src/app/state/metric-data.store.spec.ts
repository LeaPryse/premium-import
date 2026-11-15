import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../core/api.service';
import { ImportBatch, Metric, MetricField } from '../core/models';
import { MetricDataStore } from './metric-data.store';

const metric = (id: number): Metric => ({
  id, premiumMethodId: 1, name: `m${id}`, description: '', sourceType: 'excel', sourceName: 'x',
  ingestionFrequency: 'monthly', fieldCount: 1, lastImportAtUtc: null
});

const field = (id: number, key: string, dataType: MetricField['dataType']): MetricField => ({
  id, key, displayName: key, dataType, isRelevant: true, isFilterable: true, isSortable: true,
  sourceHeader: key, aliases: []
});

const emptyPage = { items: [], page: 1, pageSize: 50, totalCount: 0 };

describe('MetricDataStore', () => {
  let store: MetricDataStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    store = TestBed.inject(MetricDataStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Answers the three requests that selecting a metric triggers. */
  function answerMetricLoad(metricId: number, fields: MetricField[], batches: ImportBatch[] = []): void {
    http.expectOne(`${API_BASE_URL}/metrics/${metricId}/fields`).flush(fields);
    http.expectOne(`${API_BASE_URL}/metrics/${metricId}/imports`).flush(batches);
    http.expectOne(r => r.url === `${API_BASE_URL}/metrics/${metricId}/rows`).flush(emptyPage);
    for (const f of fields.filter(x => x.isFilterable)) {
      http.expectOne(`${API_BASE_URL}/metrics/${metricId}/fields/${f.id}/filter-options`).flush({
        fieldKey: f.key, dataType: f.dataType, operators: [], values: null, min: null, max: null
      });
    }
  }

  it('loads metrics of a method and selects the first metric', () => {
    store.selectMethod(1);
    http.expectOne(`${API_BASE_URL}/premium-methods/1/metrics`).flush([metric(7), metric(8)]);

    expect(store.selectedMetricId()).toBe(7);
    answerMetricLoad(7, [field(1, 'name', 'text'), field(2, 'amount', 'number')]);

    expect(store.visibleFields().map(f => f.key)).toEqual(['name', 'amount']);
    expect(Object.keys(store.filterOptions())).toEqual(['name', 'amount']);
  });

  it('resets page, sort and filters when another metric is selected', () => {
    store.selectMethod(1);
    http.expectOne(`${API_BASE_URL}/premium-methods/1/metrics`).flush([metric(7), metric(8)]);
    answerMetricLoad(7, [field(1, 'name', 'text')]);

    store.setFilters({ name: 'abc' });
    http.expectOne(r => r.url.endsWith('/metrics/7/rows')).flush(emptyPage);
    expect(store.query().filters).toEqual({ name: 'abc' });

    store.selectMetric(8);
    answerMetricLoad(8, [field(5, 'city', 'text')]);

    expect(store.query().filters).toEqual({});
    expect(store.query().page).toBe(1);
    expect(store.visibleFields().map(f => f.key)).toEqual(['city']);
  });

  it('ignores the response of a metric the user already left (no stale data)', () => {
    store.selectMethod(1);
    http.expectOne(`${API_BASE_URL}/premium-methods/1/metrics`).flush([metric(7), metric(8)]);
    const slowFields = http.expectOne(`${API_BASE_URL}/metrics/7/fields`);
    const slowImports = http.expectOne(`${API_BASE_URL}/metrics/7/imports`);

    store.selectMetric(8); // user switches before metric 7 answered
    expect(slowFields.cancelled).toBeTrue();
    expect(slowImports.cancelled).toBeTrue();
    answerMetricLoad(8, [field(5, 'city', 'text')]);

    expect(store.selectedMetricId()).toBe(8);
    expect(store.visibleFields().map(f => f.key)).toEqual(['city']);
  });

  it('sends filters, sort and paging to the API and resets to page 1 on change', () => {
    store.selectMethod(1);
    http.expectOne(`${API_BASE_URL}/premium-methods/1/metrics`).flush([metric(7)]);
    answerMetricLoad(7, [field(1, 'amount', 'number')]);

    store.setFilters({ amount: '10..20' });
    store.sortBy(store.fields()[0]);
    // the first rows request is cancelled by the second one (switchMap) - only the latest is answered
    const requests = http.match(r => r.url.endsWith('/metrics/7/rows'));
    const latest = requests[requests.length - 1];
    expect(latest.request.params.get('filters[amount]')).toBe('10..20');
    expect(latest.request.params.get('sortBy')).toBe('amount');
    expect(latest.request.params.get('page')).toBe('1');
    latest.flush(emptyPage);
  });

  it('toggles sort direction when the same column is clicked twice', () => {
    store.selectMethod(1);
    http.expectOne(`${API_BASE_URL}/premium-methods/1/metrics`).flush([metric(7)]);
    answerMetricLoad(7, [field(1, 'amount', 'number')]);
    const amount = store.fields()[0];

    store.sortBy(amount);
    store.sortBy(amount);
    expect(store.query().descending).toBeTrue();

    const requests = http.match(r => r.url.endsWith('/metrics/7/rows'));
    expect(requests.filter(r => r.cancelled).length).toBe(1); // the first sort request was superseded
    const latest = requests.find(r => !r.cancelled)!;
    expect(latest.request.params.get('descending')).toBe('true');
    latest.flush(emptyPage);
  });

  it('shows the server message when an upload fails', () => {
    store.selectMethod(1);
    http.expectOne(`${API_BASE_URL}/premium-methods/1/metrics`).flush([metric(7)]);
    answerMetricLoad(7, [field(1, 'amount', 'number')]);

    store.upload({ file: new File(['x'], 'a.xlsx'), reportingYear: 2026, periodType: 'quarter', periodNumber: 1 });
    http.expectOne(`${API_BASE_URL}/metrics/7/imports`).flush(
      { title: 'The file is empty.', status: 400 }, { status: 400, statusText: 'Bad Request' });
    http.expectOne(`${API_BASE_URL}/metrics/7/imports`).flush([]); // history refresh after failure

    expect(store.error()).toBe('The file is empty.');
    expect(store.uploading()).toBeFalse();
  });
});
