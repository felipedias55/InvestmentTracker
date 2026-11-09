import { TestBed } from '@angular/core/testing';
import { AssetIdentity } from './asset-identity';
describe('AssetIdentity', () => {
  function create() { const f = TestBed.createComponent(AssetIdentity); f.componentRef.setInput('assetId', 4); f.componentRef.setInput('ticker', 'TEST11'); f.componentRef.setInput('name', 'Ativo de teste'); f.detectChanges(); return f; }
  it('uses a local fallback and always keeps the name and ticker visible', () => {
    const f = create(); expect(f.nativeElement.querySelector('img')).toBeNull(); expect(f.nativeElement.textContent).toContain('Ativo de teste'); expect(f.nativeElement.textContent).toContain('TEST11');
  });
  it('loads only versioned local images and recovers from a failed load when the version changes', () => {
    const f = create(); f.componentRef.setInput('version', 'a'.repeat(64)); f.detectChanges(); const img = f.nativeElement.querySelector('img');
    expect(img.getAttribute('src')).toBe('/api/assets/4/image?v=' + 'a'.repeat(64)); expect(img.alt).toBe(''); expect(img.width).toBe(32);
    img.dispatchEvent(new Event('error')); f.detectChanges(); expect(f.nativeElement.querySelector('img')).toBeNull();
    f.componentRef.setInput('version', 'b'.repeat(64)); f.detectChanges(); expect(f.nativeElement.querySelector('img')).toBeTruthy();
    f.componentRef.setInput('version', 'https://example.com/logo.svg'); f.detectChanges(); expect(f.nativeElement.querySelector('img')).toBeNull();
  });
});
