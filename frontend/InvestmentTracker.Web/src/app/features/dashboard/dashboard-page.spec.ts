import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { DashboardPage } from './dashboard-page';
import { testDashboard, testPortfolio } from '../allocation/allocation.test-data';
import { Dashboard } from '../allocation/allocation.service';

describe('DashboardPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        brazilianLocaleProvider,
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { for (const r of http.match(req => req.url.endsWith('/history') || req.url.endsWith('/income/analysis'))) if (!r.cancelled) r.flush({ months: [], years: [], assets: [] }); http.verify(); });
  function initialize(data: Dashboard = testDashboard) {
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([testPortfolio]);
    http.expectOne('/api/portfolios/1/dashboard').flush(data);
    fixture.detectChanges();
    return fixture;
  }
  it('shows base currency totals, Brazilian percentages and comparison tables', () => {
    const fixture = initialize();
    (fixture.nativeElement.querySelector('app-allocation-table button') as HTMLButtonElement).click();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('1.234,50');
    expect(text).toContain('75,00%');
    expect(text).toContain('-25,00');
    expect(fixture.nativeElement.querySelectorAll('app-allocation-table').length).toBe(3);
    expect(fixture.nativeElement.querySelector('table').textContent).toContain('Ações');
    expect(text).toContain('Proventos são exibidos separadamente');
  });
  it('shows unavailable values without rendering null as zero', () => {
    const fixture = initialize({
      ...testDashboard,
      summary: {
        ...testDashboard.summary,
        currentValue: null,
        totalInvested: null,
        conversionAvailable: false,
      },
      allocation: {
        ...testDashboard.allocation,
        categories: [
          {
            ...testDashboard.allocation.categories[0],
            currentValue: null,
            currentPercentage: null,
            difference: null,
          },
        ],
      },
    });
    (fixture.nativeElement.querySelector('app-allocation-table button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.totals').textContent).toContain('Indisponível');
    expect(fixture.nativeElement.querySelector('table').textContent).toContain('Indisponível');
    expect(fixture.nativeElement.querySelector('meter')).toBeNull();
  });
  it('displays a fallback warning and clears old data on portfolio change', () => {
    const fixture = initialize({
      ...testDashboard,
      summary: { ...testDashboard.summary, hasFallbackRates: true },
    });
    expect(fixture.nativeElement.textContent).toContain('última cotação armazenada');
    fixture.componentInstance.select(2);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.totals')).toBeNull();
    http
      .expectOne('/api/portfolios/2/dashboard')
      .flush({}, { status: 404, statusText: 'Not Found' });
    expect(fixture.componentInstance.data()).toBeNull();
    expect(fixture.componentInstance.error()).not.toBe('');
  });
  it('filters groups without changing portfolio totals or percentages', () => {
    const fixture = initialize();
    const component = fixture.componentInstance;
    const totals = fixture.nativeElement.querySelector('.totals').textContent;
    const search: HTMLInputElement = fixture.nativeElement.querySelector('#chart-search');
    search.value = 'acoes';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(component.filteredRows('categories').length).toBe(1);
    expect(component.filteredRows('categories')[0].currentPercentage).toBe('1');
    component.status.set('below');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Nenhum grupo corresponde aos filtros');
    expect(fixture.nativeElement.querySelector('.totals').textContent).toBe(totals);
    component.clearFilters();
    fixture.detectChanges();
    expect(component.filteredRows('categories').length).toBe(1);
  });
  it('reorders and collapses charts and restores saved preferences per portfolio', () => {
    const fixture = initialize();
    const component = fixture.componentInstance;
    component.toggleOrganizing(); fixture.detectChanges();
    const move: HTMLButtonElement = fixture.nativeElement.querySelector('[aria-label="Mover Distribuição por categoria para baixo"]');
    move.click();
    fixture.detectChanges();
    expect(component.order()).toEqual(['sectors', 'categories', 'countries', 'evolution', 'income', 'highlights', 'movements']);
    const toggle: HTMLButtonElement = fixture.nativeElement.querySelector('[aria-controls="chart-categories"]');
    toggle.click();
    fixture.detectChanges();
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.nativeElement.querySelector('#chart-categories').hidden).toBe(true);
    component.select(1);
    http.expectOne('/api/portfolios/1/dashboard').flush(testDashboard);
    expect(component.order()[0]).toBe('sectors');
    expect(component.collapsed()).toEqual(['movements', 'categories']);
    component.select(2);
    http.expectOne('/api/portfolios/2/dashboard').flush(testDashboard);
    expect(component.order()[0]).toBe('categories');
    expect(component.collapsed()).toEqual(['movements']);
  });
  it('recovers from corrupt preferences and ignores unknown or duplicate chart IDs', () => {
    localStorage.setItem('investment-tracker.dashboard.v1.1', '{invalid');
    const fixture = initialize();
    const component = fixture.componentInstance;
    expect(component.order()).toEqual(['categories', 'sectors', 'countries', 'evolution', 'income', 'highlights', 'movements']);
    localStorage.setItem('investment-tracker.dashboard.v1.1', JSON.stringify({ order: ['countries', 'countries', 'unknown'], collapsed: ['unknown', 'sectors'] }));
    component.select(1);
    http.expectOne('/api/portfolios/1/dashboard').flush(testDashboard);
    expect(component.order()).toEqual(['countries', 'categories', 'sectors', 'evolution', 'income', 'highlights', 'movements']);
    expect(component.collapsed()).toEqual(['sectors', 'movements']);
    component.resetLayout();
    expect(component.collapsed()).toEqual(['movements']);
    expect(component.order()).toEqual(['categories', 'sectors', 'countries', 'evolution', 'income', 'highlights', 'movements']);
  });

  it('persists sizes and drag order, restores them and isolates portfolios', () => {
    const component = initialize().componentInstance;
    component.organizing.set(true);
    component.dragged.set('countries');
    component.dropChart({ preventDefault() {} } as DragEvent, 'categories');
    component.setSize('countries', 'wide');
    expect(component.order()).toEqual(['countries', 'categories', 'sectors', 'evolution', 'income', 'highlights', 'movements']);
    expect(component.dragged()).toBeNull();
    component.select(1);
    http.expectOne('/api/portfolios/1/dashboard').flush(testDashboard);
    expect(component.sizes().countries).toBe('wide');
    expect(component.order()[0]).toBe('countries');
    component.select(2);
    http.expectOne('/api/portfolios/2/dashboard').flush(testDashboard);
    expect(component.sizes().countries).toBe('compact');
    component.setSize('countries', 'invalid');
    expect(component.sizes().countries).toBe('compact');
    component.resetLayout();
    expect(component.order()[0]).toBe('categories');
  });
  it('ignores drops outside organization mode and preserves collapse when resized', () => {
    const component = initialize().componentInstance;
    component.dragged.set('countries');
    component.dropChart({ preventDefault() {} } as DragEvent, 'categories');
    component.moveChart('categories', 1);
    expect(component.order()[0]).toBe('categories');
    component.toggleChart('categories');
    component.setSize('categories', 'wide');
    expect(component.collapsed()).toContain('categories');
    component.toggleChart('categories');
    expect(component.sizes().categories).toBe('wide');
  });
  it('anchors a title-only drag preview to the handle and cancels without persisting an order', () => {
    const fixture = initialize(), component = fixture.componentInstance;
    component.organizing.set(true); fixture.detectChanges();
    const handle = fixture.nativeElement.querySelector('.drag-handle') as HTMLElement;
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false } as MediaQueryList)));
    const rect = vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function(this: HTMLElement) {
      return this === handle ? { left: 100, top: 200, width: 48, height: 48 } as DOMRect : { left: 0, top: 0, width: 280, height: 48 } as DOMRect;
    });
    const setDragImage = vi.fn();
    const order = [...component.order()];
    component.startDrag({ currentTarget: handle, clientX: 116, clientY: 220, dataTransfer: { setData() {}, setDragImage } } as unknown as DragEvent, 'categories');
    expect(setDragImage).toHaveBeenCalledWith(expect.any(HTMLElement), 16, 20);
    expect(setDragImage.mock.calls[0][0].textContent).toBe('Distribuição por categoria');
    component.overChart({ preventDefault() {} } as DragEvent, 'countries');
    expect(component.dropTarget()).toBe('countries');
    component.cancelDrag();
    expect(component.order()).toEqual(order); expect(component.dragged()).toBeNull();
    expect(component.dropTarget()).toBeNull(); expect(setDragImage.mock.calls[0][0].isConnected).toBe(false);
    expect(localStorage.getItem('investment-tracker.dashboard.v1.1')).toBeNull();
    rect.mockRestore(); vi.unstubAllGlobals();
  });
});
