import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CellValue, ImportedRow, MetricField } from '../../core/models';
import { MetricDataStore } from '../../state/metric-data.store';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}/;

/** Data grid whose columns come from the metric's relevant fields. Sorting and paging happen on the server. */
@Component({
  selector: 'app-dynamic-grid',
  standalone: true,
  templateUrl: './dynamic-grid.component.html',
  styleUrl: './dynamic-grid.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DynamicGridComponent {
  protected readonly store = inject(MetricDataStore);
  protected readonly pageSizes = [25, 50, 100, 200];

  protected readonly firstRow = computed(() =>
    this.store.totalCount() === 0 ? 0 : (this.store.query().page - 1) * this.store.query().pageSize + 1
  );
  protected readonly lastRow = computed(() =>
    Math.min(this.store.query().page * this.store.query().pageSize, this.store.totalCount())
  );

  protected trackRow(_: number, row: ImportedRow): number {
    return row.id;
  }

  protected isNumeric(field: MetricField): boolean {
    return field.dataType === 'number';
  }

  /** Formats a cell by the field's data type. A value that did not match the type is shown as stored (text). */
  protected format(field: MetricField, value: CellValue | undefined): string {
    if (value === null || value === undefined || value === '') {
      return '—';
    }
    switch (field.dataType) {
      case 'boolean':
        return typeof value === 'boolean' ? (value ? 'כן' : 'לא') : String(value);
      case 'number':
        return typeof value === 'number' ? value.toLocaleString('he-IL', { maximumFractionDigits: 4 }) : String(value);
      case 'date': {
        if (typeof value === 'string' && ISO_DATE.test(value)) {
          const date = new Date(value);
          if (!Number.isNaN(date.getTime())) {
            return date.toLocaleDateString('he-IL');
          }
        }
        return String(value);
      }
      default:
        return String(value);
    }
  }

  protected pageSizeChanged(event: Event): void {
    this.store.setPageSize(Number((event.target as HTMLSelectElement).value));
  }
}
