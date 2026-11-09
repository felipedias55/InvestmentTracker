import { Component, DestroyRef, effect, inject, input, output, signal, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatSliderModule } from '@angular/material/slider';
import { AssetIdentity } from '../../shared/asset-identity';
import { AssetImageChange } from './asset.service';
import { assetImageGeometry, renderAssetImage } from './normalize-asset-image';

@Component({ selector: 'app-asset-image-picker', standalone: true, imports: [MatButtonModule, MatSliderModule, AssetIdentity],
  template: `<section class="image-editor"><h3>Imagem do ativo</h3>
    <app-asset-identity [assetId]="assetId()" [version]="change() ? null : version()" [name]="name() || 'Novo ativo'" [ticker]="ticker()" [large]="true" [preview]="change()?.data ? 'data:image/png;base64,' + change()!.data : null" />
    <p class="hint">Imagem opcional. Selecione PNG, JPEG ou WebP de até 5 MB. Ajuste o zoom e a posição antes de salvar.</p>
    <div class="actions"><button mat-stroked-button type="button" [disabled]="processing()" (click)="imageFile.click()">Selecionar imagem</button>
      @if (!adjusting() && (change()?.data || version() && !change())) { <button mat-stroked-button type="button" [disabled]="processing()" (click)="adjustExisting()">Ajustar enquadramento</button> }</div>
    <input #imageFile hidden type="file" accept="image/png,image/jpeg,image/webp" [disabled]="processing()" (change)="pick($any($event.target).files?.[0]); $any($event.target).value = ''" />
    @if (processing()) { <p role="status">Preparando imagem…</p> }
    @if (error()) { <p class="notice error" role="alert">{{ error() }}</p> }
    @if (adjusting()) {
      <div class="framing"><div class="frame" role="img" aria-label="Prévia do enquadramento da imagem" [class.dragging]="dragging()" (pointerdown)="startDrag($event)" (pointermove)="drag($event)" (pointerup)="endDrag()" (pointercancel)="endDrag()" (lostpointercapture)="endDrag()">
        <img [src]="'data:image/png;base64,' + preview()" alt="" draggable="false" width="128" height="128" />
      </div><p class="hint">Arraste a imagem para posicionar e use o controle para ajustar o zoom. Áreas fora do quadrado serão cortadas; as proporções são preservadas.</p>
      <label for="asset-image-zoom">Zoom · {{ zoomPercent() }}%</label><mat-slider min="0.5" max="4" step="0.05" [disabled]="processing()"><input id="asset-image-zoom" matSliderThumb aria-label="Zoom da imagem" [value]="zoom()" (valueChange)="setZoom($event)" /></mat-slider>
      <button mat-stroked-button type="button" [disabled]="processing()" (click)="resetFrame()">Redefinir enquadramento</button>
      <p class="hint">Para recuperar detalhes cortados em uma imagem já salva, selecione novamente o arquivo original.</p></div>
    }
    <div class="actions">@if (version() || change()?.data) { <button mat-stroked-button type="button" [disabled]="processing()" (click)="remove()">Remover imagem</button> }
      @if (change()) { <button mat-stroked-button type="button" [disabled]="processing()" (click)="undo()">Desfazer alteração da imagem</button> }</div>
    <p class="hint">A alteração da imagem será aplicada ao salvar o ativo.</p>
  </section>`,
  styles: [':host { display:block; min-width:0; } .image-editor { border:1px solid var(--border); border-radius:10px; padding:16px; margin-block:16px; } .framing { margin-top:16px; } .frame { width:min(100%,224px); aspect-ratio:1; display:grid; place-items:center; overflow:hidden; border:1px solid var(--border); border-radius:8px; background:#fff; cursor:grab; touch-action:none; user-select:none; } .frame.dragging { cursor:grabbing; } .frame img { width:100%; height:100%; display:block; pointer-events:none; } label { display:block; font-weight:600; } mat-slider { width:calc(100% - 16px); margin-inline:8px; }']
})
export class AssetImagePicker {
  readonly assetId = input<number | null>(null); readonly version = input<string | null>(null); readonly name = input(''); readonly ticker = input('');
  readonly change = input<AssetImageChange | null>(null); readonly changed = output<AssetImageChange | null>(); readonly busy = output<boolean>();
  readonly processing = signal(false); readonly error = signal(''); readonly adjusting = signal(false); readonly preview = signal('');
  readonly zoom = signal(1); readonly x = signal(0); readonly y = signal(0); readonly dragging = signal(false);
  private readonly destroyRef = inject(DestroyRef); private readonly http = inject(HttpClient);
  private source?: ImageBitmap; private renderedData: string | null = null; private identity = ''; private generation = 0;
  private pointer?: { id: number; clientX: number; clientY: number; x: number; y: number; width: number };
  constructor() {
    effect(() => {
      const data = this.change()?.data ?? null, identity = `${this.assetId()}:${this.version()}`;
      untracked(() => { if (identity !== this.identity || data !== this.renderedData) this.release(); this.identity = identity; });
    });
    this.destroyRef.onDestroy(() => this.release());
  }
  zoomPercent() { return Math.round(this.zoom() * 100); }
  private release() { this.generation++; this.source?.close(); this.source = undefined; this.adjusting.set(false); this.preview.set(''); this.renderedData = null; this.endDrag(); }
  private setBusy(value: boolean) { this.processing.set(value); this.busy.emit(value); }
  async pick(file?: File) {
    if (!file || this.processing()) return;
    this.error.set('');
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024 || file.size === 0) { this.error.set('Selecione uma imagem PNG, JPEG ou WebP de até 5 MB.'); return; }
    this.setBusy(true);
    try { await this.load(file, true); }
    catch { if (!this.destroyRef.destroyed) this.error.set('Não foi possível preparar esta imagem. Tente outro arquivo.'); }
    finally { if (!this.destroyRef.destroyed) this.setBusy(false); }
  }
  async adjustExisting() {
    if (this.processing() || this.source) return;
    const generation = this.generation;
    this.error.set(''); this.setBusy(true);
    try {
      const pending = this.change()?.data;
      let blob: Blob;
      if (pending) blob = new Blob([Uint8Array.from(atob(pending), c => c.charCodeAt(0))], { type: 'image/png' });
      else {
        if (!this.assetId() || !/^[a-f0-9]{64}$/.test(this.version() ?? '') || this.change()) return;
        blob = await firstValueFrom(this.http.get(`/api/assets/${this.assetId()}/image`, { params: { v: this.version()! }, responseType: 'blob' }).pipe(takeUntilDestroyed(this.destroyRef)));
      }
      if (!this.destroyRef.destroyed && generation === this.generation) await this.load(blob, false);
    } catch { if (!this.destroyRef.destroyed) this.error.set('Não foi possível abrir a imagem para ajustar. Selecione o arquivo original.'); }
    finally { if (!this.destroyRef.destroyed) this.setBusy(false); }
  }
  private async load(blob: Blob, emit: boolean) {
    const generation = this.generation;
    const bitmap = await createImageBitmap(blob);
    let adopted = false;
    try {
      const data = renderAssetImage(bitmap, bitmap.width, bitmap.height);
      if (this.destroyRef.destroyed || generation !== this.generation) return;
      this.release(); this.source = bitmap; adopted = true; this.zoom.set(1); this.x.set(0); this.y.set(0); this.preview.set(data); this.adjusting.set(true);
      this.renderedData = emit ? data : this.change()?.data ?? null;
      if (emit) this.changed.emit({ data, expectedVersion: this.version() ?? null });
    } finally { if (!adopted) bitmap.close(); }
  }
  private draw() {
    if (!this.source || this.processing()) return;
    try {
      const data = renderAssetImage(this.source, this.source.width, this.source.height, { zoom: this.zoom(), x: this.x(), y: this.y() });
      this.preview.set(data); this.renderedData = data; this.changed.emit({ data, expectedVersion: this.version() ?? null }); this.error.set('');
    } catch { this.error.set('Não foi possível aplicar o enquadramento. Redefina ou selecione outro arquivo.'); }
  }
  setZoom(value: number) { this.zoom.set(Math.max(0.5, Math.min(4, value))); this.draw(); }
  setPosition(x: number, y: number) { this.x.set(Math.max(-100, Math.min(100, x))); this.y.set(Math.max(-100, Math.min(100, y))); this.draw(); }
  resetFrame() { this.zoom.set(1); this.x.set(0); this.y.set(0); this.draw(); }
  startDrag(event: PointerEvent) {
    if (!this.source || this.processing() || !event.isPrimary || event.button !== 0) return;
    const target = event.currentTarget as HTMLElement; const width = target.getBoundingClientRect().width; if (!width) return;
    target.setPointerCapture(event.pointerId); this.pointer = { id: event.pointerId, clientX: event.clientX, clientY: event.clientY, x: this.x(), y: this.y(), width }; this.dragging.set(true);
  }
  drag(event: PointerEvent) {
    const pointer = this.pointer; if (!pointer || pointer.id !== event.pointerId || !this.source) return;
    const geometry = assetImageGeometry(this.source.width, this.source.height, { zoom: this.zoom(), x: 0, y: 0 });
    this.setPosition(pointer.x + (event.clientX - pointer.clientX) / pointer.width * 128 / geometry.travelX * 100, pointer.y + (event.clientY - pointer.clientY) / pointer.width * 128 / geometry.travelY * 100);
  }
  endDrag() { this.pointer = undefined; this.dragging.set(false); }
  remove() { this.release(); this.changed.emit({ data: null, expectedVersion: this.version() ?? null }); }
  undo() { this.release(); this.changed.emit(null); }
}
