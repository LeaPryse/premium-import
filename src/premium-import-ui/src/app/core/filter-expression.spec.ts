import { buildFilterExpressions } from './filter-expression';
import { DataType, MetricField } from './models';

const field = (key: string, dataType: DataType): MetricField => ({
  id: 1, key, displayName: key, dataType,
  isRelevant: true, isFilterable: true, isSortable: true, sourceHeader: key, aliases: []
});

describe('buildFilterExpressions', () => {
  it('builds a between expression when both range edges are set', () => {
    const result = buildFilterExpressions([field('amount', 'number')], { amount: { from: 10, to: '20' } });
    expect(result).toEqual({ amount: '10..20' });
  });

  it('uses >= / <= when only one edge is set', () => {
    const fields = [field('amount', 'number'), field('day', 'date')];
    const result = buildFilterExpressions(fields, {
      amount: { from: '5', to: '' },
      day: { from: '', to: '2026-03-31' }
    });
    expect(result).toEqual({ amount: '>=5', day: '<=2026-03-31' });
  });

  it('keeps zero as a valid range value', () => {
    expect(buildFilterExpressions([field('amount', 'number')], { amount: { from: 0, to: null } }))
      .toEqual({ amount: '>=0' });
  });

  it('passes text and boolean values through and trims them', () => {
    const fields = [field('name', 'text'), field('active', 'boolean')];
    expect(buildFilterExpressions(fields, { name: '  נועה ', active: 'true' }))
      .toEqual({ name: 'נועה', active: 'true' });
  });

  it('omits empty controls', () => {
    const fields = [field('name', 'text'), field('amount', 'number'), field('active', 'boolean')];
    expect(buildFilterExpressions(fields, { name: '', amount: { from: '', to: '' }, active: null })).toEqual({});
  });
});
