import { ValidatorFn } from '@angular/forms';

export function formatBrazilianNumber(value: string): string {
  const [integer, fraction] = value.split('.');
  return integer.replace(/\B(?=(\d{3})+(?!\d))/g, '.') + (fraction ? ',' + fraction : '');
}

export function parseBrazilianNumber(value: string): string {
  return value.replace(/\./g, '').replace(',', '.');
}

export function brazilianNumberValidator(integerDigits: number, fractionDigits: number): ValidatorFn {
  const pattern = new RegExp(`^(?:\\d+|\\d{1,3}(?:\\.\\d{3})+)(?:,\\d{1,${fractionDigits}})?$`);
  return (control) => {
    const value = String(control.value ?? '');
    const integer = parseBrazilianNumber(value).split('.')[0];
    return pattern.test(value) && integer.length <= integerDigits ? null : { brazilianNumber: true };
  };
}
