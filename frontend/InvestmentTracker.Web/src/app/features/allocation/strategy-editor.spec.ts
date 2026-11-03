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
  it('validates the fixed-amount boundary without rounding decimal strings', () => {
    const c = create().componentInstance;
    c.rule(1, 'fixed', '999.999.999.999.999,99');
    expect(c.valid()).toBe(true);
    expect(c.config().rules[0].value).toBe('999999999999999.99');
    c.rule(1, 'fixed', '1.000.000.000.000.000');
    expect(c.valid()).toBe(false);
    c.rule(1, 'fixed', '999.999.999.999.999,999');
    expect(c.valid()).toBe(false);
    c.rule(1, 'percent', '100,01');
    expect(c.valid()).toBe(false);
  });
  it('retains all rules and exclusions when filtering or paging the editor', () => {
    const f = create(), c = f.componentInstance;
    f.componentRef.setInput('categories', Array.from({length:12}, (_, i) => ({groupId:i+1,name:'Grupo '+i,currentValue:'0',currentPercentage:'0',targetPercentage:'0',difference:'0'})));
    c.enabled.set(true); c.rule(1, 'fixed', '20,50'); c.rule(12, 'percent', '10'); c.exclude(1,true);
    c.pageIndex.set(1); f.detectChanges(); expect(c.pageCategories()).toHaveLength(2);
    c.query.set('Grupo 11'); expect(c.pageCategories()).toHaveLength(1);
    expect(c.config().rules).toEqual([{categoryId:1,kind:'fixed',value:'20.50',priority:100},{categoryId:12,kind:'percent',value:'10',priority:100}]);
    expect(c.config().excludedCategoryIds).toEqual([1]);
    c.open.set(true); f.detectChanges(); c.open.set(false); f.detectChanges();
    expect(c.config().rules).toHaveLength(2); http.expectNone(r => r.method === 'POST');
  });
  it('persists blocked pairs independently from category exclusions and restores legacy profiles', () => {
    const f = create(), c = f.componentInstance;
    c.enabled.set(true); c.block({ categoryId: 1, sectorId: 2 }, true); c.exclude(1, true);
    c.combinationQuery.set('inexistente'); c.combinationPage.set(10);
    expect(c.config().excludedCombinations).toEqual([{ categoryId: 1, sectorId: 2 }]);
    expect(c.config().excludedCategoryIds).toEqual([1]);
    c.save(); const request = http.expectOne('/api/portfolios/1/strategies');
    expect(request.request.body.config.excludedCombinations).toEqual([{ categoryId: 1, sectorId: 2 }]);
    request.flush({ profileId: request.request.body.profileId, revision: 1, config: c.config() });
    expect(c.isBlocked({ categoryId: 1, sectorId: 2 })).toBe(true);
    c.preset('custom'); expect(c.blockedCombinations()).toEqual([]);
    c.saved.set([{ profileId: 'legacy', revision: 1, config: { name: 'Antiga', model: 'regular', remainder: 'equal', rules: [], excludedCategoryIds: [], tolerance: '0' } }]);
    c.choose('legacy'); expect(c.blockedCombinations()).toEqual([]);
  });
  it('keeps a saved unavailable pair visible so it can be removed', () => {
    const c = create().componentInstance;
    c.block({ categoryId: 999, sectorId: 777 }, true);
    expect(c.combinations()[0].category).toContain('indisponível #999');
    c.block({ categoryId: 999, sectorId: 777 }, false);
    expect(c.config().excludedCombinations).toEqual([]);
  });
  it('saves and restores decimal caps independently of filters and treats a blank as no limit', () => {
    const c = create().componentInstance; c.enabled.set(true);
    c.limit('category', 1, '33,33'); c.limit('sector', 2, '0'); c.limitQuery.set('não corresponde'); c.limitPage.set(8);
    expect(c.valid()).toBe(true);
    expect(c.config().concentrationLimits).toEqual([{ dimension: 'category', groupId: 1, maximumPercentage: '33.33' }, { dimension: 'sector', groupId: 2, maximumPercentage: '0' }]);
    c.save(); const request = http.expectOne('/api/portfolios/1/strategies');
    request.flush({ profileId: request.request.body.profileId, revision: 1, config: c.config() });
    expect(c.limits()['category:1']).toBe('33,33'); expect(c.limits()['sector:2']).toBe('0');
    c.limit('sector', 2, ''); expect(c.config().concentrationLimits).toHaveLength(1);
    c.preset('custom'); expect(c.config().concentrationLimits).toEqual([]);
  });
  it('rejects invalid caps and preserves old profiles without adding a limit', () => {
    const c = create().componentInstance;
    for (const value of ['-1', '100,01', '10,001', 'texto']) { c.limit('category', 1, value); expect(c.valid()).toBe(false); }
    c.saved.set([{ profileId: 'old', revision: 1, config: { name: 'Antiga', model: 'regular', remainder: 'equal', rules: [], excludedCategoryIds: [], tolerance: '0' } }]);
    c.choose('old'); expect(c.valid()).toBe(true); expect(c.config().concentrationLimits).toEqual([]);
  });
  it('persists rule/group priorities and currency preferences without dropping them during search', () => {
    const c = create().componentInstance; c.enabled.set(true); c.usePriorities.set(true);
    c.rule(1, 'fixed', '100'); c.rulePriority(1, '2');
    c.prefer({ categoryId: 1, sectorId: 3 }, 'priority', '1'); c.prefer({ categoryId: 1, sectorId: 3 }, 'currency', 'USD');
    c.combinationQuery.set('outro filtro');
    expect(c.config().rules[0].priority).toBe(2);
    expect(c.config().groupPreferences).toEqual([{ categoryId: 1, sectorId: 3, priority: 1, preferredCurrency: 'USD' }]);
    c.save(); const request = http.expectOne('/api/portfolios/1/strategies');
    request.flush({ profileId: request.request.body.profileId, revision: 1, config: c.config() });
    expect(c.usePriorities()).toBe(true); expect(c.preference({ categoryId: 1, sectorId: 3 }).currency).toBe('USD');
    c.preset('custom'); expect(c.usePriorities()).toBe(false); expect(c.config().groupPreferences).toEqual([]);
  });
  it('rejects fractional priorities and restores the default for old configurations', () => {
    const c = create().componentInstance;
    c.rule(1, 'fixed', '10'); c.rulePriority(1, '1,5'); expect(c.valid()).toBe(false);
    c.saved.set([{ profileId: 'old', revision: 1, config: { name: 'Old', model: 'custom', remainder: 'equal', rules: [], excludedCategoryIds: [], tolerance: '0' } }]);
    c.choose('old'); expect(c.valid()).toBe(true); expect(c.usePriorities()).toBe(false); expect(c.config().groupPreferences).toEqual([]);
  });

});
