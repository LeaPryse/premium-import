import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FREQUENCY_LABELS, SOURCE_TYPE_LABELS } from '../../core/labels';
import { MetricDataStore } from '../../state/metric-data.store';

@Component({
  selector: 'app-metric-selector',
  standalone: true,
  templateUrl: './metric-selector.component.html',
  styleUrl: './metric-selector.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetricSelectorComponent {
  protected readonly store = inject(MetricDataStore);
  protected readonly sourceLabels = SOURCE_TYPE_LABELS;
  protected readonly frequencyLabels = FREQUENCY_LABELS;

  protected toId(event: Event): number | null {
    const value = Number((event.target as HTMLSelectElement).value);
    return value > 0 ? value : null;
  }
}
