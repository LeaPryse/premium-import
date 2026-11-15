import { CommonModule, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  ImportBatch,
  ImportedRow,
  Metric,
  MetricField,
  PremiumImportService,
  PremiumMethod
} from './premium-import.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, DatePipe, ReactiveFormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  readonly title = 'קליטת נתוני פרמיה';
  readonly pageSize = 25;
  readonly importForm: FormGroup;
  readonly filterForm: FormGroup;

  methods: PremiumMethod[] = [];
  metrics: Metric[] = [];
  fields: MetricField[] = [];
  rows: ImportedRow[] = [];
  batches: ImportBatch[] = [];
  selectedMethodId: number | null = null;
  selectedMetricId: number | null = null;
  selectedBatchId: number | null = null;
  selectedFile: File | null = null;
  page = 1;
  totalCount = 0;
  sortBy = '';
  descending = false;
  loading = false;
  uploading = false;
  errorMessage = '';
  successMessage = '';

  constructor(
    private readonly api: PremiumImportService,
    private readonly formBuilder: FormBuilder
  ) {
    this.importForm = this.formBuilder.group({
      reportingYear: [new Date().getFullYear(), [Validators.required, Validators.min(1900), Validators.max(2200)]],
      reportingPeriod: ['', [Validators.required, Validators.maxLength(30)]]
    });
    this.filterForm = this.formBuilder.group({});
  }

  ngOnInit(): void {
    this.loadMethods();
  }

  get visibleFields(): MetricField[] {
    return this.fields.filter(field => field.isRelevant);
  }

  get selectedMetric(): Metric | null {
    return this.metrics.find(metric => metric.id === this.selectedMetricId) ?? null;
  }

  get filterableFields(): MetricField[] {
    return this.fields.filter(field => field.isRelevant && field.isFilterable);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get firstVisibleRow(): number {
    return this.totalCount === 0 ? 0 : (this.page - 1) * this.pageSize + 1;
  }

  get lastVisibleRow(): number {
    return Math.min(this.page * this.pageSize, this.totalCount);
  }

  loadMethods(): void {
    this.errorMessage = '';
    this.api.getPremiumMethods().subscribe({
      next: methods => {
        this.methods = methods;
        if (methods.length > 0) {
          this.selectedMethodId = methods[0].id;
          this.loadMetrics();
        }
      },
      error: error => this.showError(error)
    });
  }

  onMethodChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    this.selectedMethodId = Number.isFinite(value) && value > 0 ? value : null;
    this.selectedMetricId = null;
    this.metrics = [];
    this.clearMetricData();
    if (this.selectedMethodId !== null) {
      this.loadMetrics();
    }
  }

  onMetricChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    this.selectedMetricId = Number.isFinite(value) && value > 0 ? value : null;
    this.loadMetricData();
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.successMessage = '';
    if (this.selectedFile && !/\.(xlsx|xls)$/i.test(this.selectedFile.name)) {
      this.selectedFile = null;
      input.value = '';
      this.errorMessage = 'יש לבחור קובץ Excel מסוג ‎.xlsx או ‎.xls.';
    } else {
      this.errorMessage = '';
    }
  }

  submitImport(): void {
    if (!this.selectedMetricId || !this.selectedFile || this.importForm.invalid || this.uploading) {
      this.importForm.markAllAsTouched();
      if (!this.selectedMetricId || !this.selectedFile) {
        this.errorMessage = 'יש לבחור מדד וקובץ Excel.';
      }
      return;
    }

    this.uploading = true;
    this.errorMessage = '';
    this.successMessage = '';
    const year = Number(this.importForm.controls['reportingYear'].value);
    const period = String(this.importForm.controls['reportingPeriod'].value);
    this.api.upload(this.selectedMetricId, this.selectedFile, year, period)
      .pipe(finalize(() => this.uploading = false))
      .subscribe({
        next: result => {
          this.successMessage = `הקליטה הושלמה: ${result.rowCount.toLocaleString('he-IL')} שורות, מבנה ${result.schemaVersion}${result.newSchema ? ' (מבנה חדש)' : ''}.`;
          this.selectedFile = null;
          const fileInput = document.getElementById('excel-file') as HTMLInputElement | null;
          if (fileInput) {
            fileInput.value = '';
          }
          this.selectedBatchId = result.batchId;
          this.page = 1;
          this.loadMetricData();
        },
        error: error => this.showError(error)
      });
  }

  applyFilters(): void {
    this.page = 1;
    this.loadRows();
  }

  resetFilters(): void {
    this.filterForm.reset();
    this.selectedBatchId = null;
    this.page = 1;
    this.loadRows();
  }

  onBatchChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.selectedBatchId = value ? Number(value) : null;
    this.page = 1;
    this.loadRows();
  }

  selectBatch(batchId: number): void {
    this.selectedBatchId = batchId;
    this.page = 1;
    this.loadRows();
  }

  sort(field: MetricField): void {
    if (!field.isSortable) {
      return;
    }
    if (this.sortBy === field.key) {
      this.descending = !this.descending;
    } else {
      this.sortBy = field.key;
      this.descending = false;
    }
    this.page = 1;
    this.loadRows();
  }

  changePage(direction: number): void {
    const nextPage = this.page + direction;
    if (nextPage < 1 || nextPage > this.pageCount) {
      return;
    }
    this.page = nextPage;
    this.loadRows();
  }

  displayValue(row: ImportedRow, key: string): string {
    const value = row.values[key];
    if (value === null || value === undefined || value === '') {
      return '—';
    }
    return typeof value === 'boolean' ? (value ? 'כן' : 'לא') : String(value);
  }

  trackById(_index: number, item: { id: number }): number {
    return item.id;
  }

  private loadMetrics(): void {
    if (!this.selectedMethodId) {
      return;
    }
    this.api.getMetrics(this.selectedMethodId).subscribe({
      next: metrics => {
        this.metrics = metrics;
        this.selectedMetricId = metrics[0]?.id ?? null;
        this.loadMetricData();
      },
      error: error => this.showError(error)
    });
  }

  private loadMetricData(): void {
    this.clearMetricData();
    if (!this.selectedMetricId) {
      return;
    }
    this.loading = true;
    this.api.getFields(this.selectedMetricId).subscribe({
      next: fields => {
        this.fields = fields;
        this.filterForm = this.createFilterForm(fields);
        this.api.getImports(this.selectedMetricId!).subscribe({
          next: batches => {
            this.batches = batches;
            if (this.selectedBatchId !== null && !batches.some(batch => batch.id === this.selectedBatchId)) {
              this.selectedBatchId = null;
            }
            this.loadRows();
          },
          error: error => {
            this.loading = false;
            this.showError(error);
          }
        });
      },
      error: error => {
        this.loading = false;
        this.showError(error);
      }
    });
  }

  private loadRows(): void {
    if (!this.selectedMetricId) {
      this.rows = [];
      this.totalCount = 0;
      this.loading = false;
      return;
    }
    this.loading = true;
    const filters = this.filterForm.getRawValue() as Record<string, string>;
    this.api.getRows(
      this.selectedMetricId,
      this.page,
      this.pageSize,
      filters,
      this.sortBy,
      this.descending,
      this.selectedBatchId
    ).pipe(finalize(() => this.loading = false)).subscribe({
      next: result => {
        this.rows = result.items;
        this.totalCount = result.totalCount;
      },
      error: error => this.showError(error)
    });
  }

  private createFilterForm(fields: MetricField[]): FormGroup {
    const controls: Record<string, FormControl<string>> = {};
    for (const field of fields.filter(item => item.isRelevant && item.isFilterable)) {
      controls[field.key] = this.formBuilder.nonNullable.control('');
    }
    return new FormGroup(controls);
  }

  private clearMetricData(): void {
    this.fields = [];
    this.rows = [];
    this.batches = [];
    this.totalCount = 0;
    this.page = 1;
    this.selectedBatchId = null;
    this.sortBy = '';
    this.descending = false;
    this.filterForm = new FormGroup({});
  }

  private showError(error: unknown): void {
    const response = error instanceof HttpErrorResponse ? error : null;
    const detail = response?.error;
    this.errorMessage = typeof detail === 'string'
      ? detail
      : detail?.title ?? detail?.message ?? 'אירעה שגיאה בתקשורת עם השרת. ודאו שה־API פועל ונסו שוב.';
  }
}
