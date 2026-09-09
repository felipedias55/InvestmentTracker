import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CatalogPage } from './catalog-page';

describe('CatalogPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CatalogPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { catalog: 'currencies' } } } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function page(items: unknown[] = []) {
    const fixture = TestBed.createComponent(CatalogPage);
    fixture.detectChanges();
    http.expectOne('/api/currencies').flush(items);
    fixture.detectChanges();
    return fixture;
  }
  it('shows an empty state and rejects an invalid form without sending a request', () => {
    const fixture = page();
    expect(fixture.nativeElement.textContent).toContain('Nenhum registro');
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Informe três letras');
    http.expectNone((request) => request.method === 'POST');
  });
  it('creates a currency then refreshes the list', () => {
    const fixture = page();
    fixture.componentInstance.form.setValue({ code: 'BRL', name: 'Real', symbol: 'R$' });
    fixture.componentInstance.save();
    const request = http.expectOne('/api/currencies');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.code).toBe('BRL');
    request.flush({ id: 1, code: 'BRL', name: 'Real', symbol: 'R$' });
    http.expectOne('/api/currencies').flush([{ id: 1, code: 'BRL', name: 'Real' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Cadastro salvo.');
    expect(fixture.nativeElement.querySelector('tbody').textContent).toContain('BRL');
  });
  it('updates an existing currency and preserves input when the API rejects it', () => {
    const item = { id: 1, code: 'BRL', name: 'Real' };
    const fixture = page([item]);
    fixture.componentInstance.edit(item);
    fixture.componentInstance.form.controls.code.setValue('USD');
    fixture.componentInstance.save();
    const request = http.expectOne('/api/currencies/1');
    expect(request.request.method).toBe('PUT');
    request.flush(
      { detail: 'Já existe uma moeda com esse código.' },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('Já existe');
    expect(fixture.componentInstance.form.controls.code.value).toBe('USD');
    expect(fixture.componentInstance.saving()).toBe(false);
  });
  it('requires confirmation and displays the error when deleting a referenced currency', () => {
    const item = { id: 1, code: 'BRL', name: 'Real' };
    const fixture = page([item]);
    fixture.nativeElement.querySelector('[aria-label="Excluir Real"]').click();
    fixture.detectChanges();
    http.expectNone((request) => request.method === 'DELETE');
    fixture.nativeElement.querySelector('.danger').click();
    http
      .expectOne('/api/currencies/1')
      .flush({ detail: 'A moeda está em uso.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('A moeda está em uso.');
    expect(fixture.componentInstance.items().length).toBe(1);
  });
  it('shows connection failures and allows retry', () => {
    const fixture = TestBed.createComponent(CatalogPage);
    fixture.detectChanges();
    http.expectOne('/api/currencies').error(new ProgressEvent('error'));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Não foi possível conectar');
    fixture.componentInstance.load();
    http.expectOne('/api/currencies').flush([]);
    expect(fixture.componentInstance.error()).toBe('');
  });
  it('filters a long list by name or code without changing records', () => {
    const fixture = page([{ id: 1, code: 'BRL', name: 'Real' }, { id: 2, code: 'USD', name: 'Dólar' }]);
    const search: HTMLInputElement = fixture.nativeElement.querySelector('#registration-search');
    search.value = 'dolar'; search.dispatchEvent(new Event('input')); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(1);
    expect(fixture.nativeElement.querySelector('tbody').textContent).toContain('USD');
    expect(fixture.componentInstance.items().length).toBe(2);
  });

});
