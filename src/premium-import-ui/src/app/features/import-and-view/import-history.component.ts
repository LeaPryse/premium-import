import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { STATUS_LABELS, periodLabel } from '../../core/labels';
import { ImportBatch } from '../../core/models';
import { MetricDataStore } from '../../state/metric-data.store';

/** History of uploads for the selected metric. Clicking a successful upload shows exactly that batch in the grid. */
@Component({
  selector: 'app-import-history',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './import-history.component.html',
  styleUrl: './import-history.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportHistoryComponent {
  protected readonly store = inject(MetricDataStore);
  protected readonly statusLabels = STATUS_LABELS;

  protected period(batch: ImportBatch): string {
    return `${periodLabel(batch.periodType, batch.periodNumber)} ${batch.reportingYear}`;
  }
}
