import { contributionPlan, currencyOptions, originalAmount } from './contribution-plan';
import { Contribution } from './allocation.service';
import { Position } from '../portfolio/portfolio.service';

function analysis(dimension: 'category' | 'sector', values: string[]): Contribution {
  return { dimension, currencyCode: 'BRL', amount: '100.01', unallocatedAmount: '0', sumOfWeights: '1', hasFallbackRates: false, hasStaleRates: false,
    rows: values.map((value, i) => ({ groupId: i + 1, name: `${dimension}${i}`, suggestedContribution: value, currentPercentage: '0', targetPercentage: '0.5', difference: '0.5', adjustedWeight: '0.5' })) };
}
function position(category: number, sector: number, changes: Partial<Position> = {}): Position {
  return { assetCategoryId: category, sectorId: sector, quantity: '1', currencyCode: 'USD', exchangeRate: '5', rateDate: '2026-10-06', isStale: false, isFallback: false, ...changes } as Position;
}
describe('contribution plan', () => {
  it('reroutes earlier assignments and preserves cent totals without duplicating the contribution', () => {
    const plan = contributionPlan(analysis('category', ['50', '50.01']), analysis('sector', ['50.01', '50']), [position(1, 1), position(1, 2), position(2, 1)]);
    expect(plan.unallocated).toBe('0.00');
    expect(plan.rows.map(r => [r.key, r.amount])).toEqual([['1-2', '50.00'], ['2-1', '50.01']]);
    expect(plan.rows[0].currencies[0].amount).toBe('10.00');
  });
  it('leaves incompatible budgets unallocated and excludes closed holdings', () => {
    const plan = contributionPlan(analysis('category', ['50', '50.01']), analysis('sector', ['50.01', '50']), [position(1, 1), position(2, 2, { quantity: '0' })]);
    expect(plan.rows).toHaveLength(1); expect(plan.unallocated).toBe('50.01');
  });
  it('never assigns REIT or FII outside real estate, even with inconsistent catalogs', () => {
    const plan = contributionPlan(analysis('category', ['100.01']), analysis('sector', ['100.01']), [position(1, 1, { assetCategoryName: 'REITs', sectorName: 'Tecnologia' })]);
    expect(plan.rows).toEqual([]); expect(plan.unallocated).toBe('100.01');
    expect(contributionPlan(analysis('category', ['100.01']), analysis('sector', ['100.01']), [position(1, 1, { assetCategoryName: 'FIIs', sectorName: 'Imobiliário' })]).unallocated).toBe('0.00');
  });
  it('converts in the correct direction with exact decimal rounding and flags conflicting rates', () => {
    expect(originalAmount('100.01', '5')).toBe('20.00');
    expect(originalAmount('1.00', '0.20')).toBe('5.00');
    expect(originalAmount('999999999999999.99', '1')).toBe('999999999999999.99');
    expect(originalAmount('100', '0')).toBeNull();
    const options = currencyOptions('100', [position(1, 1), position(1, 1, { exchangeRate: '6' })], 'BRL');
    expect(options[0].amount).toBeNull();
  });
});
