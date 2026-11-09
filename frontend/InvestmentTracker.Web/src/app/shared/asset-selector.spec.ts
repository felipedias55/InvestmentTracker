import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AssetChoice, AssetSelector } from './asset-selector';

const assets: AssetChoice[] = [{ id: 1, ticker: 'BANK3', name: 'Banco', currencyId: 1, imageVersion: 'a'.repeat(64) }, { id: 2, ticker: 'CARE', name: 'Saúde internacional', currencyId: 2 }];
@Component({ standalone: true, imports: [ReactiveFormsModule, AssetSelector], template: `<form [formGroup]="form"><app-asset-selector formControlName="assetId" [assets]="assets()" [currencies]="currencies" [locked]="locked()" /></form>` })
class Host {
  readonly form = new FormGroup({ assetId: new FormControl(0, { nonNullable: true, validators: [Validators.min(1)] }) });
  readonly assets = signal<readonly AssetChoice[]>(assets); currencies = [{ id: 1, code: 'BRL' }, { id: 2, code: 'USD' }]; readonly locked = signal(false);
}
describe('AssetSelector', () => {
  it('keeps the position ID as the value and uses the asset ID for its local image', () => {
    const { f, host, selector } = create(90);
    host.assets.set([{ id: 90, assetId: 12, ticker: 'POSITION', name: 'Posição zerada', currencyCode: 'USD', imageVersion: 'a'.repeat(64) }]); f.detectChanges();
    expect(f.nativeElement.querySelector('.selected img').getAttribute('src')).toContain('/api/assets/12/image');
    expect(f.nativeElement.querySelector('.selected').textContent).toContain('USD');
    selector.toggle(); selector.query.set('inexistente'); selector.close(); expect(host.form.controls.assetId.value).toBe(90);
    selector.choose(12); expect(host.form.controls.assetId.value).toBe(90);
    selector.choose(0); expect(host.form.controls.assetId.value).toBe(0);
  });
  function create(id = 0) { const f = TestBed.createComponent(Host); f.componentInstance.form.controls.assetId.setValue(id); f.detectChanges(); return { f, host: f.componentInstance, selector: f.debugElement.query(By.directive(AssetSelector)).componentInstance as AssetSelector }; }
  it('preserves the selected id while searching by normalized name and commits only an explicit choice', () => {
    const { f, host, selector } = create(1); selector.toggle(); selector.query.set('saude'); f.detectChanges();
    expect(selector.matches().map(a => a.id)).toEqual([2]); expect(host.form.controls.assetId.value).toBe(1); expect(host.form.dirty).toBe(false);
    expect(f.nativeElement.querySelector('.selected').textContent).toContain('BANK3'); expect(f.nativeElement.querySelector('.selected img').getAttribute('src')).toBe('/api/assets/1/image?v=' + 'a'.repeat(64));
    selector.choose(2); f.detectChanges(); expect(host.form.controls.assetId.value).toBe(2); expect(host.form.dirty).toBe(true); expect(host.form.touched).toBe(true); expect(selector.open()).toBe(false); expect(f.nativeElement.querySelector('.selected').textContent).toContain('USD');
  });
  it('does not alter the form when closing search and requires selection after clearing', () => {
    const { host, selector } = create(1); selector.toggle(); selector.query.set('inexistente'); selector.close(); expect(host.form.controls.assetId.value).toBe(1); expect(host.form.dirty).toBe(false);
    selector.choose(0); expect(host.form.invalid).toBe(true); expect(host.form.controls.assetId.value).toBe(0);
  });
  it('preserves disabled controls and rejects ids that are not eligible', () => {
    const { f, host, selector } = create(1); selector.choose(999); expect(host.form.controls.assetId.value).toBe(1);
    host.form.controls.assetId.disable(); f.detectChanges(); selector.choose(2); selector.toggle(); expect(host.form.getRawValue().assetId).toBe(1); expect(selector.open()).toBe(false);
    host.form.controls.assetId.enable(); host.locked.set(true); f.detectChanges(); selector.choose(2); expect(host.form.getRawValue().assetId).toBe(1);
  });
  it('resolves contextual ids after asynchronous loading without changing or dirtying the form', () => {
    const { f, host, selector } = create(2); host.assets.set([]); f.detectChanges(); expect(selector.value()).toBe(2); expect(selector.selected()).toBeUndefined();
    host.assets.set(assets); f.detectChanges(); expect(selector.selected()?.ticker).toBe('CARE'); expect(host.form.pristine).toBe(true); expect(host.form.controls.assetId.value).toBe(2);
  });
  it('limits rendered results while retaining the selected identity and allowing search outside the first page', () => {
    const { f, host, selector } = create(75); host.assets.set(Array.from({ length: 75 }, (_, i) => ({ id: i + 1, ticker: 'TEST' + String(i + 1).padStart(2, '0'), name: 'Ativo ' + i, currencyId: 1 }))); selector.toggle(); f.detectChanges();
    expect(selector.matches()).toHaveLength(75); expect(f.nativeElement.querySelectorAll('.choice')).toHaveLength(30); expect(f.nativeElement.querySelector('.selected').textContent).toContain('TEST75');
    selector.query.set('TEST75'); f.detectChanges(); expect(selector.visible().map(a => a.id)).toEqual([75]); expect(host.form.pristine).toBe(true);
  });
});
