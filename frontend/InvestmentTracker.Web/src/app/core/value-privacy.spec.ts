import { TestBed } from '@angular/core/testing';
import { PrivateCurrencyPipe, ValuePrivacy } from './value-privacy';

describe('ValuePrivacy', () => {
  afterEach(() => localStorage.removeItem('investment-tracker.hide-values'));
  it('masks monetary values and persists the preference without changing the amount', () => {
    localStorage.removeItem('investment-tracker.hide-values');
    const privacy = TestBed.inject(ValuePrivacy);
    const pipe = TestBed.runInInjectionContext(() => new PrivateCurrencyPipe());
    const original = pipe.transform('1234.56', 'BRL');
    privacy.toggle();
    expect(pipe.transform('1234.56', 'BRL')).toBe('••••••');
    expect(pipe.transform(null, 'BRL')).toBeNull();
    expect(new ValuePrivacy().hidden()).toBe(true);
    privacy.toggle();
    expect(pipe.transform('1234.56', 'BRL')).toBe(original);
  });
});
