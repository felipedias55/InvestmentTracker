import { Contribution } from './allocation.service';
import { Position } from '../portfolio/portfolio.service';

const cents = (value: string) => {
  const [whole, fraction = ''] = value.split('.');
  return BigInt(whole) * 100n + BigInt(fraction.padEnd(2, '0').slice(0, 2));
};
const money = (value: bigint) => `${value / 100n}.${String(value % 100n).padStart(2, '0')}`;

// ExchangeRate converts original currency into the portfolio base currency.
export function originalAmount(amount: string, rate: string): string | null {
  if (!/^\d+(\.\d+)?$/.test(rate)) return null;
  const [whole, fraction = ''] = rate.split('.');
  const divisor = BigInt(whole + fraction);
  if (divisor <= 0n) return null;
  const numerator = cents(amount) * 10n ** BigInt(fraction.length);
  return money((numerator + divisor / 2n) / divisor);
}

export function currencyOptions(amount: string, positions: Position[], base: string) {
  return [...new Set(positions.map(p => p.currencyCode))].sort().map(currency => {
    const sources = positions.filter(p => p.currencyCode === currency);
    const rates = new Set(sources.map(p => p.exchangeRate));
    const rate = currency === base ? '1' : rates.size === 1 ? sources[0].exchangeRate : null;
    return { currency, amount: rate ? originalAmount(amount, rate) : null, rate,
      dates: [...new Set(sources.map(p => p.rateDate).filter(Boolean))].join(', '),
      stale: sources.some(p => p.isStale || p.isFallback) };
  });
}

export function contributionPlan(category: Contribution, sector: Contribution, positions: Position[]) {
  const cats = [...category.rows].sort((a, b) => a.groupId - b.groupId);
  const secs = [...sector.rows].sort((a, b) => a.groupId - b.groupId);
  const size = cats.length + secs.length + 2, sink = size - 1;
  const capacity = Array.from({ length: size }, () => Array<bigint>(size).fill(0n));
  const eligible = positions.filter(p => {
    if (Number(p.quantity) <= 0) return false;
    const realEstateOnly = /\b(fii|fiis|reit|reits)\b/i.test(`${p.assetCategoryName} ${p.assetTypeName}`);
    return !realEstateOnly || /imobili|real estate/i.test(p.sectorName ?? '');
  });
  cats.forEach((c, i) => capacity[0][i + 1] = cents(c.suggestedContribution));
  secs.forEach((s, j) => capacity[cats.length + j + 1][sink] = cents(s.suggestedContribution));
  cats.forEach((c, i) => secs.forEach((s, j) => {
    if (eligible.some(p => p.assetCategoryId === c.groupId && p.sectorId === s.groupId))
      capacity[i + 1][cats.length + j + 1] = cents(category.amount);
  }));
  // Integral max flow: residual paths can undo an earlier assignment to avoid greedy dead ends.
  let allocated = 0n;
  while (true) {
    const parent = Array<number>(size).fill(-1), queue = [0]; parent[0] = 0;
    for (let k = 0; k < queue.length && parent[sink] < 0; k++) {
      const node = queue[k];
      for (let next = 1; next < size; next++) if (parent[next] < 0 && capacity[node][next] > 0n) {
        parent[next] = node; queue.push(next);
      }
    }
    if (parent[sink] < 0) break;
    let flow = cents(category.amount);
    for (let v = sink; v !== 0; v = parent[v]) if (capacity[parent[v]][v] < flow) flow = capacity[parent[v]][v];
    for (let v = sink; v !== 0; v = parent[v]) { capacity[parent[v]][v] -= flow; capacity[v][parent[v]] += flow; }
    allocated += flow;
  }
  const rows = cats.flatMap((c, i) => secs.flatMap((s, j) => {
    const value = capacity[cats.length + j + 1][i + 1];
    if (!value) return [];
    const matches = eligible.filter(p => p.assetCategoryId === c.groupId && p.sectorId === s.groupId);
    return [{ key: `${c.groupId}-${s.groupId}`, category: c.name, sector: s.name, amount: money(value),
      currencies: currencyOptions(money(value), matches, category.currencyCode) }];
  }));
  return { rows, unallocated: money(cents(category.amount) - allocated) };
}
