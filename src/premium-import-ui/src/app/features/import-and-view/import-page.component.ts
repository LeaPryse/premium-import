import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { periodLabel } from '../../core/labels';
import { ImportBatch } from '../../core/models';
import { MetricDataStore } from '../../state/metric-data.store';
import { DynamicFiltersComponent } from './dynamic-filters.component';
import { DynamicGridComponent } from './dynamic-grid.component';
import { ImportFormComponent } from './import-form.component';
import { ImportHistoryComponent } from './import-history.component';
import { MetricSelectorComponent } from './metric-selector.component';

/** Screen 3: choose a metric, upload a file, and browse the imported data. Composition only - logic lives in the store. */
@Component({
  selector: 'app-import-page',
  standalone: true,
  imports: [
    MetricSelectorComponent,
    ImportFormComponent,
    DynamicFiltersComponent,
    DynamicGridComponent,
    ImportHistoryComponent
  ],
  templateUrl: './import-page.component.html',
  styleUrl: './import-page.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportPageComponent implements OnInit {
  protected readonly store = inject(MetricDataStore);

  ngOnInit(): void {
    this.store.init();
  }

  protected batchLabel(batch: ImportBatch): string {
    const superseded = batch.isSuperseded ? ' · הוחלפה' : '';
    return `${periodLabel(batch.periodType, batch.periodNumber)} ${batch.reportingYear} · ${batch.originalFileName} · ${batch.rowCount.toLocaleString('he-IL')} שורות${superseded}`;
  }

  protected onBatchChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.store.setBatch(value ? Number(value) : null);
  }
}
