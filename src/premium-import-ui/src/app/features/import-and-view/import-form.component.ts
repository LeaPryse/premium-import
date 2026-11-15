import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  effect,
  inject,
  input,
  output,
  signal,
  untracked
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { PERIOD_TYPES, maxPeriodNumber, periodOptions } from '../../core/labels';
import { ImportRequest, PeriodType } from '../../core/models';

const ALLOWED_EXTENSIONS = /\.(xlsx|xls|csv)$/i;

function fileValidator(control: AbstractControl<File | null>): ValidationErrors | null {
  const file = control.value;
  if (!file) {
    return { required: true };
  }
  return ALLOWED_EXTENSIONS.test(file.name) ? null : { extension: true };
}

/**
 * Reactive form for uploading a file. The period-number options and its max validator
 * follow the selected period type (month 1-12, quarter 1-4, half-year 1-2, year).
 */
@Component({
  selector: 'app-import-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './import-form.component.html',
  styleUrl: './import-form.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportFormComponent {
  private readonly destroyRef = inject(DestroyRef);

  readonly uploading = input(false);
  readonly disabled = input(false);
  /** The calculation period of the premium method - used as the default period type. */
  readonly defaultPeriodType = input<PeriodType>('quarter');
  readonly submitted = output<ImportRequest>();

  @ViewChild('fileInput') private fileInput?: ElementRef<HTMLInputElement>;

  protected readonly periodTypes = PERIOD_TYPES;
  protected readonly periodNumbers = signal(periodOptions('quarter'));

  protected readonly form = new FormGroup({
    reportingYear: new FormControl(new Date().getFullYear(), {
      nonNullable: true,
      validators: [Validators.required, Validators.min(2000), Validators.max(2100)]
    }),
    periodType: new FormControl<PeriodType>('quarter', { nonNullable: true }),
    periodNumber: new FormControl(1, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(1), Validators.max(4)]
    }),
    file: new FormControl<File | null>(null, fileValidator)
  });

  constructor() {
    this.form.controls.periodType.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(type => this.applyPeriodType(type));

    // Follow the premium method's calculation period whenever it changes.
    effect(() => {
      const type = this.defaultPeriodType();
      untracked(() => this.form.controls.periodType.setValue(type));
    });
  }

  protected onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.form.controls.file.setValue(file);
    this.form.controls.file.markAsTouched();
  }

  protected submit(): void {
    if (this.form.invalid || this.uploading() || this.disabled()) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.submitted.emit({
      file: value.file!,
      reportingYear: value.reportingYear,
      periodType: value.periodType,
      periodNumber: value.periodNumber
    });
    this.form.controls.file.reset(null);
    if (this.fileInput) {
      this.fileInput.nativeElement.value = '';
    }
  }

  private applyPeriodType(type: PeriodType): void {
    const max = maxPeriodNumber(type);
    const number = this.form.controls.periodNumber;
    this.periodNumbers.set(periodOptions(type));
    number.setValidators([Validators.required, Validators.min(1), Validators.max(max)]);
    if (number.value > max) {
      number.setValue(1);
    }
    number.updateValueAndValidity();
  }
}
