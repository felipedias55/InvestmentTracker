import { formatBrazilianNumber, parseBrazilianNumber } from '../../core/brazilian-number';

// Exact decimal shifts between the percentage shown in a field and the fraction used by the API.
export function percentageUnits(value: string): bigint {
  const [whole, fraction = ''] = parseBrazilianNumber(value).split('.');
  return BigInt(whole || '0') * 10000n + BigInt(fraction.padEnd(4, '0'));
}
export function percentageToFraction(value: string): string {
  const units = percentageUnits(value);
  return `${units / 1000000n}.${(units % 1000000n).toString().padStart(6, '0')}`;
}
export function fractionToPercentage(value: string): string {
  const [whole, fraction = ''] = value.split('.');
  const units = BigInt(whole) * 1000000n + BigInt(fraction.padEnd(6, '0'));
  return formatBrazilianNumber(`${units / 10000n}.${(units % 10000n).toString().padStart(4, '0')}`);
}
