import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, switchMap } from 'rxjs';
import { FilterControlValue, buildFilterExpressions } from '../../core/filter-expression';
import { MetricField } from '../../core/models';
import { MetricDataStore } from '../../state/metric-data.store';

/**
 * Filter panel generated from the metric's field list:
 *   text -> free text (contains) with suggestions   number/date -> from..to range   boolean -> yes/no
 * Changes are debounced and pushed to the store as filter expressions, which are executed in the database.
 */
@Component({
  selector: 'app-dynamic-filters',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './dynamic-filters.component.html',
  styleUrl: './dynamic-filters.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DynamicFiltersComponent {
  protected readonly store = inject(MetricDataStore);

  /** A new form is derived whenever the metric (or its structure) changes - old filter values are dropped. */
  protected readonly form = computed(() => this.buildForm(this.store.filterableFields()));

  constructor() {
    toObservable(this.form)
      .pipe(
        switchMap(form => form.valueChanges.pipe(debounceTime(400))),
        takeUntilDestroyed()
      )
      .subscribe(value =>
        this.store.setFilters(
          buildFilterExpressions(this.store.filterableFields(), value as Record<string, FilterControlValue | undefined>)
        )
      );
  }

  protected group(key: string): FormGroup {
    return this.form().controls[key] as FormGroup;
  }

  protected suggestions(field: MetricField): string[] {
    const values = this.store.filterOptions()[field.key]?.values ?? [];
    return values.map(v => String(v.value));
  }

  protected rangeHint(field: MetricField, edge: 'min' | 'max'): string {
    const value = this.store.filterOptions()[field.key]?.[edge];
    const prefix = edge === 'min' ? 'מ-' : 'עד ';
    if (value === null || value === undefined) {
      return prefix.trim();
    }
    const text = field.dataType === 'date' ? String(value).slice(0, 10) : Number(value).toLocaleString('he-IL');
    return prefix + text;
  }

  protected reset(): void {
    this.form().reset();
  }

  protected hasActiveFilters(): boolean {
    return Object.keys(this.store.query().filters).length > 0;
  }

  private buildForm(fields: MetricField[]): FormGroup<Record<string, AbstractControl>> {
    const controls: Record<string, AbstractControl> = {};
    for (const field of fields) {
      controls[field.key] =
        field.dataType === 'number' || field.dataType === 'date'
          ? new FormGroup({ from: new FormControl(''), to: new FormControl('') })
          : new FormControl('');
    }
    return new FormGroup(controls);
  }
}
