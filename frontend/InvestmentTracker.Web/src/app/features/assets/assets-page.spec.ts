import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AssetsPage } from './assets-page';

describe('AssetsPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssetsPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function flushLoad(empty = false) {
    http.expectOne('/api/assets').flush([]);
    for (const key of ['asset-types', 'countries', 'currencies', 'asset-categories', 'sectors']) {
      http.expectOne('/api/' + key).flush(empty ? [] : [{ id: 1, name: 'Classificação' }]);
    }
  }
  it('requires all classifications before submitting', () => {
    const fixture = TestBed.createComponent(AssetsPage);
    fixture.detectChanges();
    flushLoad();
    fixture.componentInstance.newItem();
    fixture.componentInstance.form.patchValue({ ticker: 'PETR4', name: 'Petrobras' });
    fixture.componentInstance.save();
    fixture.detectChanges();
    http.expectNone((request) => request.method === 'POST');
    expect(fixture.nativeElement.textContent).toContain('Selecione moeda.');
  });
  it('creates an asset with the selected independent references', () => {
    const fixture = TestBed.createComponent(AssetsPage);
    fixture.detectChanges();
    flushLoad();
    fixture.componentInstance.newItem();
    fixture.componentInstance.form.setValue({
      ticker: 'PETR4',
      name: 'Petrobras',
      assetTypeId: 1,
      countryId: 1,
      currencyId: 1,
      assetCategoryId: 1,
      sectorId: 1,
    });
    fixture.componentInstance.save();
    const request = http.expectOne('/api/assets');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.currencyId).toBe(1);
    expect(request.request.body.createdAt).toBeUndefined();
    request.flush({ id: 1, ...request.request.body, createdAt: '2026-09-07T00:00:00Z' });
    flushLoad();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ativo salvo.');
  });
  it('links to missing catalog registrations', () => {
    const fixture = TestBed.createComponent(AssetsPage);
    fixture.detectChanges();
    flushLoad(true);
    fixture.componentInstance.newItem();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a[href="/currencies"]')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Cadastre primeiro');
  });
  it('retains the form on a validation error', () => {
    const fixture = TestBed.createComponent(AssetsPage);
    fixture.detectChanges();
    flushLoad();
    fixture.componentInstance.newItem();
    fixture.componentInstance.form.setValue({
      ticker: 'PETR4',
      name: 'Petrobras',
      assetTypeId: 1,
      countryId: 1,
      currencyId: 1,
      assetCategoryId: 1,
      sectorId: 1,
    });
    fixture.componentInstance.save();
    http
      .expectOne('/api/assets')
      .flush(
        { detail: 'Selecione uma moeda existente.' },
        { status: 400, statusText: 'Bad Request' },
      );
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain(
      'Selecione uma moeda existente.',
    );
    expect(fixture.componentInstance.form.controls.ticker.value).toBe('PETR4');
  });
  it('filters before pagination and keeps the current page while editing', () => {
    const fixture = TestBed.createComponent(AssetsPage); fixture.detectChanges(); flushLoad();
    const component = fixture.componentInstance;
    const assets = Array.from({ length: 26 }, (_, i) => ({ id: i + 1, ticker: 'TEST' + String(i).padStart(2, '0'), name: 'Ativo', assetTypeId: 1, countryId: 1, currencyId: 1, assetCategoryId: i < 12 ? 1 : 2, sectorId: 1, createdAt: '2026-10-01T12:00:00Z' }));
    component.items.set(assets); component.categoryFilter.set('1');
    expect(component.filteredItems()).toHaveLength(12);
    component.changePage({ pageIndex: 1, pageSize: 10 }); expect(component.pageItems()).toHaveLength(2);
    component.edit(component.pageItems()[0]); component.cancel(); expect(component.currentPage()).toBe(1);
    component.search('TEST00'); expect(component.currentPage()).toBe(0); expect(component.pageItems()).toHaveLength(1);
    expect(component.items()).toHaveLength(26);
  });

});
