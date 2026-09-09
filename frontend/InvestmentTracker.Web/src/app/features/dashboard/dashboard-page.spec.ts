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
  afterEach(() => http.verify());
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
    const move: HTMLButtonElement = fixture.nativeElement.querySelector('[aria-label="Mover Distribuição por categoria para baixo"]');
    move.click();
    fixture.detectChanges();
    expect(component.order()).toEqual(['sectors', 'categories', 'countries']);
    const toggle: HTMLButtonElement = fixture.nativeElement.querySelector('[aria-controls="chart-categories"]');
    toggle.click();
    fixture.detectChanges();
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.nativeElement.querySelector('#chart-categories').hidden).toBe(true);
    component.select(1);
    http.expectOne('/api/portfolios/1/dashboard').flush(testDashboard);
    expect(component.order()[0]).toBe('sectors');
    expect(component.collapsed()).toEqual(['categories']);
    component.select(2);
    http.expectOne('/api/portfolios/2/dashboard').flush(testDashboard);
    expect(component.order()[0]).toBe('categories');
    expect(component.collapsed()).toEqual([]);
  });
  it('recovers from corrupt preferences and ignores unknown or duplicate chart IDs', () => {
    localStorage.setItem('investment-tracker.dashboard.v1.1', '{invalid');
    const fixture = initialize();
    const component = fixture.componentInstance;
    expect(component.order()).toEqual(['categories', 'sectors', 'countries']);
    localStorage.setItem('investment-tracker.dashboard.v1.1', JSON.stringify({ order: ['countries', 'countries', 'unknown'], collapsed: ['unknown', 'sectors'] }));
    component.select(1);
    http.expectOne('/api/portfolios/1/dashboard').flush(testDashboard);
    expect(component.order()).toEqual(['countries', 'categories', 'sectors']);
    expect(component.collapsed()).toEqual(['sectors']);
    component.resetLayout();
    expect(component.collapsed()).toEqual([]);
    expect(component.order()).toEqual(['categories', 'sectors', 'countries']);
  });

});
