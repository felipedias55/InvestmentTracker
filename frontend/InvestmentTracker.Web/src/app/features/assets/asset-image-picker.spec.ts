import { TestBed } from '@angular/core/testing';
import { AssetImagePicker } from './asset-image-picker';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { assetImageGeometry } from './normalize-asset-image';
describe('AssetImagePicker', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
  it('rejects unsupported or oversized files before decoding', async () => {
    const f = TestBed.createComponent(AssetImagePicker); f.detectChanges(); const changed = vi.fn(); f.componentInstance.changed.subscribe(changed); const decode = vi.fn(); vi.stubGlobal('createImageBitmap', decode);
    await f.componentInstance.pick(new File(['<svg/>'], 'logo.svg', { type: 'image/svg+xml' })); expect(changed).not.toHaveBeenCalled(); expect(decode).not.toHaveBeenCalled(); expect(f.componentInstance.error()).toContain('PNG, JPEG ou WebP');
  });
  it('normalizes selected images and preserves the expected version for atomic saving', async () => {
    const f = TestBed.createComponent(AssetImagePicker); f.componentRef.setInput('version', 'a'.repeat(64)); f.detectChanges(); const changed = vi.fn(); f.componentInstance.changed.subscribe(changed);
    const close = vi.fn(), drawImage = vi.fn(); vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue({ width: 200, height: 100, close }));
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage } as unknown as CanvasRenderingContext2D); vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue('data:image/png;base64,cG5n');
    await f.componentInstance.pick(new File(['png'], 'logo.png', { type: 'image/png' })); expect(changed).toHaveBeenCalledWith({ data: 'cG5n', expectedVersion: 'a'.repeat(64) }); expect(close).not.toHaveBeenCalled(); expect(f.componentInstance.processing()).toBe(false);
    expect(drawImage).toHaveBeenLastCalledWith(expect.anything(), 0, 32, 128, 64);
    f.componentInstance.setZoom(2); f.componentInstance.setPosition(50, -25);
    expect(drawImage).toHaveBeenLastCalledWith(expect.anything(), 0, -16, 256, 128);
    f.componentInstance.resetFrame(); expect(drawImage).toHaveBeenLastCalledWith(expect.anything(), 0, 32, 128, 64);
    f.destroy(); expect(close).toHaveBeenCalledOnce();
  });
  it('opens an existing image locally without changing it until its framing is adjusted', async () => {
    const f = TestBed.createComponent(AssetImagePicker); f.componentRef.setInput('assetId', 9); f.componentRef.setInput('version', 'a'.repeat(64)); f.detectChanges();
    const changed = vi.fn(); f.componentInstance.changed.subscribe(changed); vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue({ width: 128, height: 128, close: vi.fn() }));
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage: vi.fn() } as unknown as CanvasRenderingContext2D); vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue('data:image/png;base64,cG5n');
    const loading = f.componentInstance.adjustExisting(); const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(r => r.url === '/api/assets/9/image'); expect(request.request.params.get('v')).toBe('a'.repeat(64)); request.flush(new Blob(['png'], { type: 'image/png' })); await loading;
    expect(changed).not.toHaveBeenCalled(); expect(f.componentInstance.adjusting()).toBe(true);
    f.componentInstance.setPosition(10, 20); expect(changed).toHaveBeenLastCalledWith({ data: 'cG5n', expectedVersion: 'a'.repeat(64) }); http.verify();
  });
  it('drops the previous original when an external image is applied and preserves it after invalid file selection', async () => {
    const f = TestBed.createComponent(AssetImagePicker); f.detectChanges(); const close = vi.fn();
    vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue({ width: 100, height: 100, close })); vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage: vi.fn() } as unknown as CanvasRenderingContext2D); vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue('data:image/png;base64,cG5n');
    await f.componentInstance.pick(new File(['png'], 'logo.png', { type: 'image/png' }));
    await f.componentInstance.pick(new File(['svg'], 'bad.svg', { type: 'image/svg+xml' })); expect(close).not.toHaveBeenCalled(); expect(f.componentInstance.adjusting()).toBe(true);
    f.componentRef.setInput('change', { data: 'b3RoZXI=', expectedVersion: null }); f.detectChanges(); expect(close).toHaveBeenCalledOnce(); expect(f.componentInstance.adjusting()).toBe(false);
  });
  it('bounds the zoom and position while preserving aspect ratio', () => {
    const frame = assetImageGeometry(400, 200, { zoom: 2, x: 50, y: -25 }); expect(frame).toMatchObject({ width: 256, height: 128, x: 0, y: -16 });
    expect(assetImageGeometry(400, 200, { zoom: 99, x: 999, y: -999 })).toEqual(assetImageGeometry(400, 200, { zoom: 4, x: 100, y: -100 }));
    expect(() => assetImageGeometry(0, 100, { zoom: 1, x: 0, y: 0 })).toThrow();
  });
  it('does not replace a newer pending image when a previous file finishes decoding', async () => {
    const f = TestBed.createComponent(AssetImagePicker); f.detectChanges(); const changed = vi.fn(); f.componentInstance.changed.subscribe(changed); const close = vi.fn();
    let finish!: (bitmap: ImageBitmap) => void;
    vi.stubGlobal('createImageBitmap', vi.fn().mockReturnValue(new Promise<ImageBitmap>(resolve => { finish = resolve; })));
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage: vi.fn() } as unknown as CanvasRenderingContext2D); vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue('data:image/png;base64,cG5n');
    const pending = f.componentInstance.pick(new File(['png'], 'first.png', { type: 'image/png' }));
    f.componentRef.setInput('change', { data: 'bmV3', expectedVersion: null }); f.detectChanges();
    finish({ width: 128, height: 128, close } as unknown as ImageBitmap); await pending;
    expect(changed).not.toHaveBeenCalled(); expect(close).toHaveBeenCalledOnce(); expect(f.componentInstance.adjusting()).toBe(false);
  });
});
