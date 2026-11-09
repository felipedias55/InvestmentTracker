import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AssetDiscovery, DiscoveredAsset } from './asset-discovery';

describe('AssetDiscovery', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [AssetDiscovery], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => { http.verify(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
  function create() {
    const f = TestBed.createComponent(AssetDiscovery); f.componentRef.setInput('current', { ticker: 'LOCAL', name: 'Meu nome', currencyId: 1, countryId: 2, assetTypeId: 1, sectorId: 1, assetCategoryId: 5 });
    f.componentRef.setInput('references', { currencies: [{ id: 1, name: 'Real', code: 'BRL' }, { id: 2, name: 'Dólar', code: 'USD' }], countries: [{ id: 2, name: 'Brasil' }], 'asset-types': [{ id: 1, name: 'Ação', instrumentClass: 'general' }], sectors: [{ id: 1, name: 'Energia' }] }); f.detectChanges(); return f;
  }
  const item: DiscoveredAsset = { symbol: 'TEST', name: 'Nome da fonte', market: 'US', currency: 'USD', country: 'USA', sector: 'Technology', instrumentClass: 'general', hasImage: true };
  it('searches only after an explicit action and applies only checked differences without assigning strategy categories', () => {
    const f = create(), applied = vi.fn(); f.componentInstance.appliedFields.subscribe(applied); http.expectNone(() => true);
    f.componentInstance.market.set('US'); f.componentInstance.query.set('TEST'); f.componentInstance.search();
    const request = http.expectOne(r => r.url === '/api/asset-discovery'); expect(request.request.params.get('market')).toBe('US'); request.flush({ provider: 'Synthetic', status: 'available', message: 'Review', items: [item] });
    expect(applied).not.toHaveBeenCalled(); f.componentInstance.select(item); const rows = f.componentInstance.differences();
    expect(rows.find(r => r.field === 'name')!.checked).toBe(false); expect(rows.some(r => r.field === 'countryId')).toBe(false); expect(rows.some(r => r.field === 'sectorId')).toBe(false);
    rows.find(r => r.field === 'name')!.checked = true; f.componentInstance.apply();
    expect(applied).toHaveBeenCalledWith({ name: item.name, listing: { market: 'US', symbol: 'TEST' } });
  });
  it('preserves confirmed images and does not apply a downloaded image until explicitly selected', async () => {
    const f = create(), applied = vi.fn(); f.componentInstance.appliedFields.subscribe(applied); f.componentRef.setInput('imageVersion', 'a'.repeat(64)); f.componentInstance.select(item);
    vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue({ width: 128, height: 128, close: vi.fn() })); vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage: vi.fn() } as unknown as CanvasRenderingContext2D); vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue('data:image/png;base64,cG5n');
    f.componentInstance.fetchImage(); http.expectOne(r => r.url === '/api/asset-discovery/image').flush({ status: 'available', message: 'Review', contentType: 'image/png', data: 'cG5n' }); await new Promise(resolve => setTimeout(resolve, 0));
    f.componentInstance.apply(); expect(applied.mock.lastCall![0].image).toBeUndefined();
    f.componentRef.setInput('current', { ...f.componentInstance.current(), ...applied.mock.lastCall![0] }); f.detectChanges();
    f.componentInstance.useImage.set(true); f.componentInstance.apply(); expect(applied.mock.lastCall![0].image).toEqual({ data: 'cG5n', expectedVersion: 'a'.repeat(64) });
  });
  it('keeps the current draft available after provider errors and clears discarded results', () => {
    const f = create(); f.componentInstance.query.set('TEST'); f.componentInstance.search(); http.expectOne(r => r.url === '/api/asset-discovery').flush({ detail: 'Fonte indisponível' }, { status: 503, statusText: 'Unavailable' });
    expect(f.componentInstance.error()).toContain('Fonte indisponível'); expect(f.componentInstance.current().name).toBe('Meu nome'); expect(f.componentInstance.busy()).toBe(false); f.componentInstance.clear(); expect(f.componentInstance.selected()).toBeNull();
  });
  it('requires reviewing again when a selected field was edited while suggestions were open', () => {
    const f = create(), applied = vi.fn(); f.componentInstance.appliedFields.subscribe(applied); f.componentInstance.select(item);
    f.componentInstance.differences().find(row => row.field === 'name')!.checked = true;
    f.componentRef.setInput('current', { ...f.componentInstance.current(), name: 'Nome digitado depois' }); f.detectChanges(); f.componentInstance.apply();
    expect(applied).not.toHaveBeenCalled(); expect(f.componentInstance.error()).toContain('formulário mudou');
    expect(f.componentInstance.differences().find(row => row.field === 'name')!.current).toBe('Nome digitado depois');
    expect(f.componentInstance.differences().find(row => row.field === 'name')!.checked).toBe(false);
  });
  it('prepares a translated sector without applying it even when the current sector is empty', () => {
    const f = create(), applied = vi.fn(); f.componentInstance.appliedFields.subscribe(applied);
    f.componentRef.setInput('current', { ...f.componentInstance.current(), sectorId: 0 });
    f.componentRef.setInput('references', { sectors: [{ id: 8, name: 'Tecnologia' }] }); f.detectChanges();
    f.componentInstance.select(item); expect(f.componentInstance.sectorMessage()).toContain('Equivalência');
    f.detectChanges();
    expect((f.nativeElement.querySelectorAll('select')[1] as HTMLSelectElement).value).toBe('8');
    const sector = f.componentInstance.differences().find(row => row.field === 'sectorId')!;
    expect(sector.value).toBe(8); expect(sector.checked).toBe(false);
    f.componentInstance.apply(); expect(applied.mock.lastCall![0].sectorId).toBeUndefined();
    f.componentRef.setInput('current', { ...f.componentInstance.current(), ...applied.mock.lastCall![0] }); f.detectChanges();
    sector.checked = true; f.componentInstance.apply(); expect(applied.mock.lastCall![0].sectorId).toBe(8);
    http.expectNone(() => true);
  });
  it('allows resolving ambiguous or absent sectors and discarding the proposed choice', () => {
    const f = create(); f.componentRef.setInput('references', { sectors: [{ id: 8, name: 'Tecnologia' }, { id: 9, name: 'Technology' }] }); f.detectChanges();
    f.componentInstance.select(item); expect(f.componentInstance.sectorMessage()).toContain('Mais de um');
    expect(f.componentInstance.sectorChoice()).toBe(0); expect(f.componentInstance.differences().some(row => row.field === 'sectorId')).toBe(false);
    f.componentInstance.reviewSector(9); expect(f.componentInstance.differences().find(row => row.field === 'sectorId')!.checked).toBe(false);
    f.componentInstance.reviewSector(0); expect(f.componentInstance.differences().some(row => row.field === 'sectorId')).toBe(false);
    f.componentInstance.select({ ...item, sector: null }); expect(f.componentInstance.sectorMessage()).toContain('não informou');
    f.componentInstance.reviewSector(8); expect(f.componentInstance.sectorChoice()).toBe(8);
    f.componentInstance.clear(); expect(f.componentInstance.sectorChoice()).toBe(0); expect(f.componentInstance.current().sectorId).toBe(1);
  });
  it('rejects a checked sector removed or renamed during review', () => {
    const f = create(), applied = vi.fn(); f.componentInstance.appliedFields.subscribe(applied);
    f.componentInstance.select({ ...item, sector: 'Energy' });
    f.componentInstance.differences().find(row => row.field === 'sectorId')!.checked = true;
    f.componentRef.setInput('references', { sectors: [{ id: 1, name: 'Outra classificação' }] }); f.detectChanges();
    f.componentInstance.apply(); expect(applied).not.toHaveBeenCalled(); expect(f.componentInstance.error()).toContain('catálogo de setores mudou');
    expect(f.componentInstance.sectorChoice()).toBe(0);
  });
  it('resets the rendered confirmation when changing the proposed sector', () => {
    const f = create(), c = f.componentInstance;
    f.componentRef.setInput('references', { sectors: [{ id: 8, name: 'Tecnologia' }, { id: 9, name: 'Technology' }] }); f.detectChanges();
    c.select(item); c.reviewSector(8); f.detectChanges();
    c.checkField('sectorId', true); f.detectChanges();
    expect((f.nativeElement.querySelector('input[aria-label="Aplicar sugestão de Setor"]') as HTMLInputElement).checked).toBe(true);
    c.reviewSector(9); f.detectChanges();
    expect((f.nativeElement.querySelector('input[aria-label="Aplicar sugestão de Setor"]') as HTMLInputElement).checked).toBe(false);
    expect(c.differences().find(row => row.field === 'sectorId')!.value).toBe(9);
  });
});
