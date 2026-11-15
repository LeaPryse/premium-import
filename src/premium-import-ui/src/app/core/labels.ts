import { Frequency, ImportStatus, PeriodType, SourceType } from './models';

const MONTHS = [
  'ינואר', 'פברואר', 'מרץ', 'אפריל', 'מאי', 'יוני',
  'יולי', 'אוגוסט', 'ספטמבר', 'אוקטובר', 'נובמבר', 'דצמבר'
];

export const PERIOD_TYPES: { value: PeriodType; label: string }[] = [
  { value: 'month', label: 'חודש' },
  { value: 'quarter', label: 'רבעון' },
  { value: 'halfYear', label: 'מחצית' },
  { value: 'year', label: 'שנה' }
];

export const maxPeriodNumber = (type: PeriodType): number =>
  ({ month: 12, quarter: 4, halfYear: 2, year: 1 })[type];

/** "רבעון 1", "אפריל", "מחצית 2", "שנתי" */
export function periodLabel(type: PeriodType, number: number): string {
  switch (type) {
    case 'month': return MONTHS[number - 1] ?? `חודש ${number}`;
    case 'quarter': return `רבעון ${number}`;
    case 'halfYear': return `מחצית ${number}`;
    default: return 'שנתי';
  }
}

export const periodTypeLabel = (type: PeriodType): string =>
  PERIOD_TYPES.find(p => p.value === type)?.label ?? type;

export const periodOptions = (type: PeriodType): { value: number; label: string }[] =>
  Array.from({ length: maxPeriodNumber(type) }, (_, i) => ({ value: i + 1, label: periodLabel(type, i + 1) }));

export const SOURCE_TYPE_LABELS: Record<SourceType, string> = {
  excel: 'קובץ Excel',
  externalApi: 'ממשק למערכת חיצונית'
};

export const FREQUENCY_LABELS: Record<Frequency, string> = {
  daily: 'יומית',
  weekly: 'שבועית',
  monthly: 'חודשית',
  quarterly: 'רבעונית',
  yearly: 'שנתית'
};

export const STATUS_LABELS: Record<ImportStatus, string> = {
  processing: 'בעיבוד',
  completed: 'הושלמה',
  failed: 'נכשלה'
};
