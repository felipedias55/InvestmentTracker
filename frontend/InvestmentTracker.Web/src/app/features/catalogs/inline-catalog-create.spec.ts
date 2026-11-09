import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InlineCatalogCreate } from './inline-catalog-create';
describe('InlineCatalogCreate', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [InlineCatalogCreate], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  it('creates a sector through the catalog service and returns its classification', () => {
    const f = TestBed.createComponent(InlineCatalogCreate); f.componentRef.setInput('catalog', 'sectors'); f.detectChanges(); const c = f.componentInstance; const created = vi.fn(); c.created.subscribe(created);
    c.form.patchValue({ name: 'Imóveis', isRealEstate: true }); c.save(); const request = http.expectOne('/api/sectors'); expect(request.request.body).toEqual({ name: 'Imóveis', isRealEstate: true });
    request.flush({ id: 7, name: 'Imóveis', isRealEstate: true }); expect(created).toHaveBeenCalledWith({ id: 7, name: 'Imóveis', isRealEstate: true });
  });
  it('validates currency fields and preserves input after duplicate errors', () => {
    const f = TestBed.createComponent(InlineCatalogCreate); f.componentRef.setInput('catalog', 'currencies'); f.detectChanges(); const c = f.componentInstance;
    c.form.controls.name.setValue('Euro'); c.save(); http.expectNone('/api/currencies'); c.form.patchValue({ code: 'EUR', symbol: '€' }); c.save();
    http.expectOne('/api/currencies').flush({ detail: 'Moeda já existe' }, { status: 409, statusText: 'Conflict' }); expect(c.form.controls.name.value).toBe('Euro'); expect(c.error()).toContain('já existe');
  });
});
