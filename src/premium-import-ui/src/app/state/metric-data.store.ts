import { Injectable, computed, inject, signal } from '@angular/core';
import { Subject, catchError, forkJoin, of, switchMap, tap } from 'rxjs';
import { ApiService, describeError } from '../core/api.service';
import {
  FieldFilterOptions,
  ImportBatch,
  ImportedRow,
  ImportRequest,
  Metric,
  MetricField,
  PremiumMethod,
  RowsQuery
} from '../core/models';

const DEFAULT_PAGE_SIZE = 50;
const emptyQuery = (pageSize = DEFAULT_PAGE_SIZE): RowsQuery => ({
  batchId: null,
  page: 1,
  pageSize,
  sortBy: '',
  descending: false,
  filters: {}
});

/**
 * Single source of truth for screen 3 (import + view). Components read signals and call intent methods;
 * HTTP and race handling live here. Every "load" stream uses switchMap, so a slow response of a metric
 * the user already left can never overwrite the data of the current one.
 */
@Injectable({ providedIn: 'root' })
export class MetricDataStore {
  private readonly api = inject(ApiService);

  // ---- state ----
  readonly methods = signal<PremiumMethod[]>([]);
  readonly metrics = signal<Metric[]>([]);
  readonly fields = signal<MetricField[]>([]);
  readonly batches = signal<ImportBatch[]>([]);
  readonly filterOptions = signal<Record<string, FieldFilterOptions>>({});
  readonly rows = signal<ImportedRow[]>([]);
  readonly totalCount = signal(0);
  readonly query = signal<RowsQuery>(emptyQuery());

  readonly selectedMethodId = signal<number | null>(null);
  readonly selectedMetricId = signal<number | null>(null);

  readonly loadingRows = signal(false);
  readonly uploading = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  // ---- derived ----
  readonly selectedMethod = computed(() => this.methods().find(m => m.id === this.selectedMethodId()) ?? null);
  readonly selectedMetric = computed(() => this.metrics().find(m => m.id === this.selectedMetricId()) ?? null);
  /** Fields the user chose as relevant for the metric - these are the grid columns. */
  readonly visibleFields = computed(() => this.fields().filter(f => f.isRelevant));
  readonly filterableFields = computed(() => this.fields().filter(f => f.isRelevant && f.isFilterable));
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.query().pageSize)));
  readonly completedBatches = computed(() => this.batches().filter(b => b.status === 'completed'));

  // ---- request streams ----
  private readonly methodSelected$ = new Subject<number>();
  private readonly metricSelected$ = new Subject<{ metricId: number; batchId: number | null }>();
  private readonly optionsRequested$ = new Subject<{ metricId: number; fields: MetricField[] }>();
  private readonly rowsRequested$ = new Subject<void>();

  constructor() {
    this.methodSelected$
      .pipe(switchMap(id => this.api.getMetrics(id).pipe(catchError(err => this.fail(err, [] as Metric[])))))
      .subscribe(metrics => {
        this.metrics.set(metrics);
        this.loadMetric(metrics[0]?.id ?? null);
      });

    this.metricSelected$
      .pipe(
        switchMap(({ metricId, batchId }) =>
          forkJoin({ fields: this.api.getFields(metricId), batches: this.api.getImports(metricId) }).pipe(
            tap(() => this.query.set({ ...emptyQuery(this.query().pageSize), batchId })),
            catchError(err => this.fail(err, null))
          )
        )
      )
      .subscribe(result => {
        if (!result) {
          this.loadingRows.set(false);
          return;
        }
        this.fields.set(result.fields);
        this.batches.set(result.batches);
        this.rowsRequested$.next();
        this.optionsRequested$.next({ metricId: this.selectedMetricId()!, fields: result.fields });
      });

    this.optionsRequested$
      .pipe(
        switchMap(({ metricId, fields }) => {
          const filterable = fields.filter(f => f.isRelevant && f.isFilterable);
          if (filterable.length === 0) {
            return of([] as FieldFilterOptions[]);
          }
          return forkJoin(filterable.map(f => this.api.getFilterOptions(metricId, f.id))).pipe(
            // Options only enrich the filters (suggestions, min/max) - failing here must not break the screen.
            catchError(() => of([] as FieldFilterOptions[]))
          );
        })
      )
      .subscribe(options => this.filterOptions.set(Object.fromEntries(options.map(o => [o.fieldKey, o]))));

    this.rowsRequested$
      .pipe(
        tap(() => this.loadingRows.set(true)),
        switchMap(() => {
          const metricId = this.selectedMetricId();
          if (metricId === null) {
            return of(null);
          }
          return this.api.getRows(metricId, this.query()).pipe(catchError(err => this.fail(err, null)));
        })
      )
      .subscribe(page => {
        this.rows.set(page?.items ?? []);
        this.totalCount.set(page?.totalCount ?? 0);
        this.loadingRows.set(false);
      });
  }

  // ---- intents ----

  init(): void {
    this.api
      .getPremiumMethods()
      .pipe(catchError(err => this.fail(err, [] as PremiumMethod[])))
      .subscribe(methods => {
        this.methods.set(methods);
        if (methods.length > 0) {
          this.selectMethod(methods[0].id);
        }
      });
  }

  selectMethod(methodId: number | null): void {
    this.selectedMethodId.set(methodId);
    this.clearMetricData();
    this.selectedMetricId.set(null);
    this.metrics.set([]);
    if (methodId !== null) {
      this.methodSelected$.next(methodId);
    }
  }

  selectMetric(metricId: number | null): void {
    this.loadMetric(metricId);
  }

  setBatch(batchId: number | null): void {
    this.updateQuery({ batchId });
  }

  setFilters(filters: Record<string, string>): void {
    this.updateQuery({ filters });
  }

  setPageSize(pageSize: number): void {
    this.updateQuery({ pageSize });
  }

  sortBy(field: MetricField): void {
    if (!field.isSortable) {
      return;
    }
    const { sortBy, descending } = this.query();
    this.updateQuery(sortBy === field.key ? { descending: !descending } : { sortBy: field.key, descending: false });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.pageCount() || page === this.query().page) {
      return;
    }
    this.query.update(q => ({ ...q, page }));
    this.rowsRequested$.next();
  }

  upload(request: ImportRequest): void {
    const metricId = this.selectedMetricId();
    if (metricId === null || this.uploading()) {
      return;
    }
    this.uploading.set(true);
    this.error.set('');
    this.success.set('');
    this.api.upload(metricId, request).subscribe({
      next: result => {
        this.uploading.set(false);
        const added = result.newFields.length > 0 ? ` נוספו שדות: ${result.newFields.join(', ')}.` : '';
        this.success.set(
          `הקליטה הושלמה: ${result.rowCount.toLocaleString('he-IL')} שורות, גרסת מבנה ${result.schemaVersion}` +
            `${result.newSchema ? ' (מבנה חדש)' : ''}.${added}`
        );
        // Reload fields/history/options (the structure may have changed) and show the new batch.
        this.loadMetric(metricId, result.batchId);
      },
      error: err => {
        this.uploading.set(false);
        this.error.set(describeError(err));
        // A failed import is still recorded in the history.
        this.api.getImports(metricId).subscribe(batches => this.batches.set(batches));
      }
    });
  }

  dismissMessages(): void {
    this.error.set('');
    this.success.set('');
  }

  // ---- internals ----

  private loadMetric(metricId: number | null, batchId: number | null = null): void {
    this.selectedMetricId.set(metricId);
    if (metricId === null) {
      this.clearMetricData();
      return;
    }
    this.loadingRows.set(true);
    this.metricSelected$.next({ metricId, batchId });
  }

  private updateQuery(change: Partial<RowsQuery>): void {
    this.query.update(q => ({ ...q, ...change, page: 1 }));
    this.rowsRequested$.next();
  }

  private clearMetricData(): void {
    this.fields.set([]);
    this.batches.set([]);
    this.filterOptions.set({});
    this.rows.set([]);
    this.totalCount.set(0);
    this.query.update(q => emptyQuery(q.pageSize));
    this.loadingRows.set(false);
  }

  private fail<T>(error: unknown, fallback: T) {
    this.error.set(describeError(error));
    return of(fallback);
  }
}
