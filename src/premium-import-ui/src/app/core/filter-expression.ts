import { MetricField } from './models';

/** Value of one filter control: a string for text/boolean, a {from,to} pair for number/date. */
export type FilterControlValue = string | number | null | { from?: string | number | null; to?: string | number | null };

const clean = (value: unknown): string => (value === null || value === undefined ? '' : String(value).trim());

/**
 * Converts the filter form value to the expression syntax understood by the API:
 *   range (number/date):  "10..20" | ">=10" | "<=20"
 *   text:                 "abc"  (contains)
 *   boolean:              "true" | "false"
 * Empty controls are omitted.
 */
export function buildFilterExpressions(
  fields: MetricField[],
  formValue: Record<string, FilterControlValue | undefined>
): Record<string, string> {
  const expressions: Record<string, string> = {};

  for (const field of fields) {
    const raw = formValue[field.key];
    if (raw === null || raw === undefined) {
      continue;
    }

    if (field.dataType === 'number' || field.dataType === 'date') {
      const range = typeof raw === 'object' ? raw : {};
      const from = clean(range.from);
      const to = clean(range.to);
      if (from && to) {
        expressions[field.key] = `${from}..${to}`;
      } else if (from) {
        expressions[field.key] = `>=${from}`;
      } else if (to) {
        expressions[field.key] = `<=${to}`;
      }
    } else {
      const text = clean(raw);
      if (text) {
        expressions[field.key] = text;
      }
    }
  }
  return expressions;
}
