import { afterNextRender, Component, computed, ElementRef, forwardRef, Injector, inject, input, output, QueryList, signal, ViewChild, ViewChildren } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AssetIdentity } from './asset-identity';

export interface AssetChoice { id: number; assetId?: number; name: string; ticker: string; currencyId?: number; currencyCode?: string; imageVersion?: string | null; }
const normalize = (value: string) => value.trim().normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');

@Component({ selector: 'app-asset-selector', standalone: true,
  imports: [AssetIdentity, MatButtonModule, MatFormFieldModule, MatInputModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => AssetSelector), multi: true }],
  template: `<section class="selector" aria-label="Seleção do ativo">
    <p class="label">{{ label() }}</p>
    @if (selected(); as asset) { <div class="selected" role="status"><app-asset-identity [assetId]="asset.assetId ?? asset.id" [version]="asset.imageVersion" [name]="asset.name" [ticker]="asset.ticker" /><span class="currency">{{ currency(asset) }}</span></div> }
    @else { <p class="hint">{{ value() ? 'O ativo selecionado não está disponível na lista atual.' : emptyLabel() }}</p> }
    <div class="actions"><button #opener mat-stroked-button type="button" [disabled]="disabled()" [attr.aria-expanded]="open()" (click)="toggle()">{{ value() ? 'Trocar ativo' : 'Selecionar ativo' }}</button>
      @if (value()) { <button mat-button type="button" [disabled]="disabled()" (click)="choose(0)">Limpar seleção do ativo</button> }</div>
    @if (open()) { <div class="search-panel" (keydown)="keyboard($event)">
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Buscar ativo por código ou nome</mat-label><input #searchBox matInput type="search" autocomplete="off" [value]="query()" [disabled]="disabled()" (input)="query.set($any($event.target).value)" /></mat-form-field>
      <p class="hint" role="status">{{ matches().length }} {{ matches().length === 1 ? 'ativo encontrado.' : 'ativos encontrados.' }}{{ matches().length > limit ? ' Exibindo os primeiros ' + limit + '; refine a busca.' : '' }}</p>
      <div class="results" role="region" aria-label="Resultados da busca de ativos"><ul>@for (asset of visible(); track asset.id) { <li><button #result mat-button class="choice" type="button" [disabled]="disabled()" [attr.aria-label]="'Selecionar ' + asset.ticker + ' — ' + asset.name" [attr.aria-pressed]="asset.id === value()" (click)="choose(asset.id)">
        <span class="choice-content"><app-asset-identity [assetId]="asset.assetId ?? asset.id" [version]="asset.imageVersion" [name]="asset.name" [ticker]="asset.ticker" /><span class="currency">{{ currency(asset) }}</span></span>
      </button></li> } @empty { <li class="empty">Nenhum ativo corresponde à busca. Confira o código ou nome.</li> }</ul></div>
      <button mat-button type="button" (click)="close()">Fechar busca de ativos</button>
    </div> }
  </section>`,
  styles: [':host { display:block; min-width:0; margin-block:8px 20px; } .selector { min-width:0; } .label { font-weight:600; margin:0 0 8px; } .selected { display:flex; align-items:center; gap:12px; padding:12px; border:1px solid var(--border); border-radius:8px; } app-asset-identity { flex:1; min-width:0; } .currency { font-size:12px; white-space:nowrap; color:var(--muted); } .search-panel { border:1px solid var(--border); border-radius:8px; padding:12px; margin-top:8px; min-width:0; } mat-form-field { width:100%; min-width:0; } .results { max-height:280px; overflow:auto; overscroll-behavior:contain; } ul { list-style:none; padding:0; margin:0; } .choice { display:flex; width:100%; height:auto; min-height:64px; padding:12px; white-space:normal; text-align:left; justify-content:flex-start; } .choice[aria-pressed=true] { background:var(--bg); } .choice-content { display:flex; align-items:center; gap:12px; min-width:0; width:100%; } .empty { padding:12px; }']
})
export class AssetSelector implements ControlValueAccessor {
  readonly assets = input<readonly AssetChoice[]>([]); readonly currencies = input<readonly { id: number; code?: string }[]>([]); readonly locked = input(false);
  readonly label = input('Ativo'); readonly emptyLabel = input('Nenhum ativo selecionado.');
  readonly selectionChanged = output<number>();
  readonly value = signal(0); readonly open = signal(false); readonly query = signal(''); private readonly formDisabled = signal(false);
  readonly disabled = computed(() => this.locked() || this.formDisabled()); readonly selected = computed(() => this.assets().find(a => a.id === this.value())); readonly limit = 30;
  readonly matches = computed(() => { const query = normalize(this.query()); return this.assets().filter(a => normalize(a.ticker + ' ' + a.name).includes(query)).slice().sort((a, b) => a.ticker.localeCompare(b.ticker, 'pt-BR') || a.id - b.id); });
  readonly visible = computed(() => this.matches().slice(0, this.limit));
  private readonly injector = inject(Injector);
  @ViewChild('opener', { read: ElementRef }) opener?: ElementRef<HTMLButtonElement>;
  @ViewChild('searchBox', { read: ElementRef }) searchBox?: ElementRef<HTMLInputElement>;
  @ViewChildren('result', { read: ElementRef }) results?: QueryList<ElementRef<HTMLButtonElement>>;
  private changed: (value: number) => void = () => undefined; private touched: () => void = () => undefined;
  writeValue(value: number | null) { this.value.set(Number(value) || 0); this.open.set(false); this.query.set(''); }
  registerOnChange(fn: (value: number) => void) { this.changed = fn; }
  registerOnTouched(fn: () => void) { this.touched = fn; }
  setDisabledState(value: boolean) { this.formDisabled.set(value); }
  currency(asset: AssetChoice) { return asset.currencyCode ?? this.currencies().find(c => c.id === asset.currencyId)?.code ?? ''; }
  toggle() {
    if (this.disabled()) return;
    if (this.open()) { this.close(); return; }
    this.query.set(''); this.open.set(true);
    afterNextRender(() => this.searchBox?.nativeElement.focus(), { injector: this.injector });
  }
  close() { this.open.set(false); this.touched(); afterNextRender(() => this.opener?.nativeElement.focus(), { injector: this.injector }); }
  choose(id: number) {
    if (this.disabled() || id !== 0 && !this.assets().some(a => a.id === id)) return;
    if (id !== this.value()) { this.value.set(id); this.changed(id); this.selectionChanged.emit(id); }
    this.close();
  }
  keyboard(event: KeyboardEvent) {
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); this.close(); return; }
    if (this.disabled()) return;
    const buttons = this.results?.toArray().map(r => r.nativeElement) ?? [];
    if (event.key === 'Enter' && event.target === this.searchBox?.nativeElement) { event.preventDefault(); if (this.matches().length === 1) this.choose(this.matches()[0].id); else buttons[0]?.focus(); }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault(); if (!buttons.length) return; const index = buttons.findIndex(b => b === event.target); const next = event.key === 'ArrowDown' ? (index + 1) % buttons.length : index <= 0 ? buttons.length - 1 : index - 1; buttons[next]?.focus();
    }
  }
}
