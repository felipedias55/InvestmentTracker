import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StrategyEditor, strategyModels } from './strategy-editor';

describe('StrategyEditor', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [StrategyEditor], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function create() {
    const f = TestBed.createComponent(StrategyEditor);
    f.componentRef.setInput('portfolioId', 1); f.componentRef.setInput('currency', 'BRL'); f.detectChanges();
    http.expectOne('/api/portfolios/1/strategies').flush([]); f.detectChanges(); return f;
  }
  it('includes buy and hold and requires explicit fixed real estate categories', () => {
    const f = create(), c = f.componentInstance;
    expect(strategyModels.some(m => m.id === 'buy-hold')).toBe(true);
    c.enabled.set(true); c.preset('real-estate');
    expect(c.valid()).toBe(false);
    c.rule(1, 'fixed', '500,00'); c.exclude(1,true);
    expect(c.valid()).toBe(true);
    expect(c.config().rules[0].value).toBe('500.00');
    c.rule(2, 'percent', '101');
    expect(c.valid()).toBe(false);
    c.rule(2, '', '');
    c.save();
    const request = http.expectOne('/api/portfolios/1/strategies');
    expect(request.request.body.config.excludedCategoryIds).toEqual([1]);
    request.flush({ profileId: request.request.body.profileId, revision: 1, config: c.config() });
    expect(c.revision()).toBe(1);
    http.expectNone(r => r.url.includes('/trades') || r.url.includes('/movements'));
  });
  it('restores percentages without floating point artifacts and starts a new profile for another model', () => {
    const c = create().componentInstance;
    c.saved.set([{ profileId: 'test', revision: 2, config: { name: 'Faixas', model: 'bands', remainder: 'targets', excludedCategoryIds: [], rules: [], tolerance: '0.07' } }]);
    c.choose('test');
    expect(c.tolerance()).toBe('7,00');
    expect(c.config().tolerance).toBe('0.0700');
    expect(c.valid()).toBe(true);
    c.preset('buy-hold');
    expect(c.profileId()).toBe('');
    expect(c.remainder()).toBe('targets');
  });
});
