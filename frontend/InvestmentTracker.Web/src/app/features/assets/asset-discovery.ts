import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AssetIdentity } from '../../shared/asset-identity';
import { apiError } from '../../core/services/api-error';
import { CatalogItem } from '../catalogs/catalog.models';
import { AssetInput } from './asset.service';
import { normalizeAssetImage } from './normalize-asset-image';
import { matchSector } from './sector-matching';

export interface DiscoveredAsset { symbol: string; name: string; market: string; currency: string | null; country: string | null; instrumentClass: string | null; sector: string | null; hasImage: boolean; }
export interface DiscoveryResult { provider: string; status: string; message: string; items: DiscoveredAsset[]; fromCache: boolean; retrievedAt: string | null; retryAfter: string | null; usage: { attempts: number; limit: number; windowDays: number } | null; }
type Field = 'ticker' | 'name' | 'currencyId' | 'countryId' | 'assetTypeId' | 'sectorId' | 'listing';
interface Difference { field: Field; label: string; current: string; suggested: string; value: string | number | { market: string; symbol: string }; checked: boolean; baseline: string; }
const normalize = (value: string) => value.trim().normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase();

@Component({ selector: 'app-asset-discovery', standalone: true,
  imports: [DatePipe, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule, AssetIdentity],
  template: `<details class="discovery"><summary>Buscar por ticker e revisar sugestões</summary>
    <p class="hint">Escolha o mercado. A busca não salva o ativo. Buscar símbolos e consultar imagem nos EUA consome consultas separadas, compartilhadas com as cotações.</p>
    <div class="search-fields"><mat-form-field appearance="outline"><mat-label>Mercado da busca</mat-label><select matNativeControl [value]="market()" (change)="market.set($any($event.target).value); clear()" [disabled]="busy()"><option value="BR">Brasil · B3</option><option value="US">EUA · USD</option></select></mat-form-field>
    <mat-form-field appearance="outline"><mat-label>Ticker para buscar</mat-label><input matInput maxlength="20" [value]="query()" (input)="query.set($any($event.target).value); clear()" [disabled]="busy()" (keydown.enter)="$event.preventDefault(); search()" /></mat-form-field></div>
    <button mat-stroked-button type="button" (click)="search()" [disabled]="busy() || !query().trim()">Buscar sugestões</button>
    @if (busy()) { <p role="status">Consultando fonte…</p> }
    @if (error()) { <p class="notice error" role="alert">{{ error() }}</p> }
    @if (result(); as result) { <p role="status">{{ result.message }}</p><p class="hint">Fonte: {{ result.provider }}{{ result.fromCache ? ' · cache' : '' }}@if (result.retrievedAt) { · consultado em {{ result.retrievedAt | date:'dd/MM/yyyy HH:mm' }} }</p>
      @if (result.usage) { <p class="hint">Controle local: {{ result.usage.attempts }}/{{ result.usage.limit }} consultas em {{ result.usage.windowDays }} dias.</p> }
      @if (result.retryAfter) { <p class="notice">Tente novamente após {{ result.retryAfter | date:'dd/MM/yyyy HH:mm' }}. Você pode continuar manualmente.</p> }
      <div class="results">@for (item of result.items; track item.symbol) { <button mat-stroked-button class="result" type="button" (click)="select(item)" [disabled]="busy()"><app-asset-identity [ticker]="item.symbol" [name]="item.name" [preview]="selected()?.symbol === item.symbol && image() ? 'data:image/png;base64,' + image() : null" /><span>{{ item.market }} · {{ item.currency || 'Moeda pendente' }}</span></button> }</div>
    }
    @if (selected(); as selected) {
      <h4>Revisar {{ selected.symbol }}</h4><p class="hint">Marque somente os campos que deseja aplicar. A categoria da estratégia é escolhida no formulário. O país não é deduzido da bolsa; tipo e setor sem correspondência única precisam de seleção manual.</p>
      <div class="table-scroll" tabindex="0" role="region" aria-label="Diferenças do cadastro"><table class="differences-table"><thead><tr><th>Campo</th><th>Atual</th><th>Sugestão</th><th>Aplicar</th></tr></thead><tbody>@for (row of differences(); track row.field + (row.field === 'sectorId' ? ':' + row.value : '')) { <tr><th scope="row">{{ row.label }}</th><td class="current">{{ row.current || 'Não preenchido' }}</td><td class="suggested">{{ row.suggested }}</td><td class="apply"><mat-checkbox [checked]="row.checked" (change)="checkField(row.field, $event.checked)" [aria-label]="'Aplicar sugestão de ' + row.label" /></td></tr> }</tbody></table></div>
      <p class="hint">País informado pela fonte: {{ selected.country || 'Não informado' }} · Tipo: {{ selected.instrumentClass || 'Revisar manualmente' }} · Setor: {{ selected.sector || 'Não informado' }}</p>
      <p class="hint" role="status">{{ sectorMessage() }}</p>
      <mat-form-field appearance="outline"><mat-label>Setor para revisar</mat-label><select matNativeControl (change)="reviewSector(+$any($event.target).value)" [disabled]="busy()"><option value="0" [selected]="sectorChoice() === 0">Manter setor atual</option>@for (sector of references()['sectors'] || []; track sector.id) { <option [value]="sector.id" [selected]="sectorChoice() === sector.id">{{ sector.name }}</option> }</select></mat-form-field>
      <p class="hint">Escolher um setor prepara a comparação. Marque Aplicar sugestão de Setor e aplique os campos para alterar o rascunho. Salvar ativo confirma a alteração.</p>
      @if (selected.hasImage) { <button mat-stroked-button type="button" (click)="fetchImage()" [disabled]="busy()">Buscar imagem sugerida</button> }
      @if (imageMessage()) { <p role="status">{{ imageMessage() }}</p> }
      @if (image()) { <app-asset-identity [ticker]="selected.symbol" [name]="selected.name" [preview]="'data:image/png;base64,' + image()" /><mat-checkbox [checked]="useImage()" (change)="useImage.set($event.checked)">Aplicar imagem sugerida (substitui a atual)</mat-checkbox> }
      <div class="actions"><button mat-flat-button type="button" (click)="apply()" [disabled]="busy()">Aplicar campos selecionados</button><button mat-button type="button" (click)="clear()" [disabled]="busy()">Descartar sugestões</button></div>
    }
    @if (applied()) { <p role="status">Sugestões aplicadas ao formulário. Confira os campos e salve o ativo para confirmar.</p> }
  </details>`,
  styles: [`:host { display:block; min-width:0; } .discovery { border:1px solid var(--border); border-radius:10px; padding:16px; margin-bottom:20px; } summary { cursor:pointer; } .search-fields { display:grid; grid-template-columns:repeat(auto-fit,minmax(min(100%,180px),1fr)); gap:12px; margin-top:16px; } .results { display:grid; gap:10px; margin-block:16px; } .result { height:auto; min-height:64px; white-space:normal; text-align:left; padding:12px; justify-content:flex-start; } table { width:100%; } th,td { text-align:left; padding:8px; overflow-wrap:anywhere; } .table-scroll { overflow-x:auto; }
    @media (max-width:600px) {
      .differences-table thead { position:absolute; width:1px; height:1px; overflow:hidden; clip-path:inset(50%); }
      .differences-table tbody { display:grid; gap:12px; }
      .differences-table tr { display:grid; grid-template-columns:minmax(0,1fr) auto; border:1px solid var(--border); border-radius:8px; padding:8px; }
      .differences-table th { grid-column:1/-1; }
      .differences-table td { border:0; }
      .differences-table .current,.differences-table .suggested { grid-column:1; }
      .differences-table .current::before { content:'Atual: '; font-weight:600; }
      .differences-table .suggested::before { content:'Sugestão: '; font-weight:600; }
      .differences-table .apply { grid-column:2; grid-row:2/4; align-self:center; }
    }`]
})
export class AssetDiscovery {
  readonly current = input.required<AssetInput>(); readonly references = input.required<Record<string, CatalogItem[]>>(); readonly imageVersion = input<string | null>(null);
  readonly appliedFields = output<Partial<AssetInput>>();
  private readonly http = inject(HttpClient); private readonly destroyRef = inject(DestroyRef);
  readonly market = signal('BR'); readonly query = signal(''); readonly busy = signal(false); readonly error = signal(''); readonly applied = signal(false);
  readonly result = signal<DiscoveryResult | null>(null); readonly selected = signal<DiscoveredAsset | null>(null); readonly differences = signal<Difference[]>([]);
  readonly image = signal<string | null>(null); readonly imageMessage = signal(''); readonly useImage = signal(false);
  readonly sectorMessage = signal(''); readonly sectorChoice = signal(0);
  private imageBaseline = 'null';
  clear() { this.result.set(null); this.selected.set(null); this.differences.set([]); this.image.set(null); this.imageMessage.set(''); this.useImage.set(false); this.sectorMessage.set(''); this.sectorChoice.set(0); this.error.set(''); this.applied.set(false); }
  search() {
    if (this.busy() || !this.query().trim()) return;
    this.clear(); this.busy.set(true);
    this.http.get<DiscoveryResult>('/api/asset-discovery', { params: { market: this.market(), query: this.query().trim() } }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => { this.result.set(result); this.busy.set(false); }, error: error => { this.error.set(apiError(error)); this.busy.set(false); } });
  }
  select(item: DiscoveredAsset) {
    this.selected.set(item); this.image.set(null); this.imageMessage.set(''); this.useImage.set(false); this.applied.set(false);
    const current = this.current(), refs = this.references(); const rows: Difference[] = [];
    const add = (field: Field, label: string, value: Difference['value'], suggested: string, currentLabel: string) => rows.push({ field, label, value, suggested, current: currentLabel, checked: !current[field], baseline: JSON.stringify(current[field] ?? null) });
    add('ticker', 'Ticker', item.symbol, item.symbol, current.ticker); add('name', 'Nome', item.name, item.name, current.name);
    add('listing', 'Mercado e símbolo de consulta', { market: item.market, symbol: item.symbol }, item.market + ' · ' + item.symbol, current.listing ? current.listing.market + ' · ' + current.listing.symbol : 'Compatibilidade com o cadastro atual');
    const match = (key: string, predicate: (c: CatalogItem) => boolean) => { const values = (refs[key] ?? []).filter(predicate); return values.length === 1 ? values[0] : null; };
    const fields: [Field, string, string, CatalogItem | null][] = [
      ['currencyId', 'Moeda', 'currencies', match('currencies', c => !!item.currency && normalize(c.code ?? '') === normalize(item.currency))],
      ['countryId', 'País', 'countries', match('countries', c => !!item.country && (normalize(c.name) === normalize(item.country) || ['USA', 'UNITED STATES', 'UNITED STATES OF AMERICA'].includes(normalize(item.country)) && ['ESTADOS UNIDOS', 'USA', 'UNITED STATES', 'UNITED STATES OF AMERICA'].includes(normalize(c.name))))],
      ['assetTypeId', 'Tipo de ativo', 'asset-types', match('asset-types', c => !!item.instrumentClass && c.instrumentClass === item.instrumentClass)],
    ];
    for (const [field, label, key, value] of fields) if (value) add(field, label, value.id, value.name, (refs[key] ?? []).find(c => c.id === current[field])?.name ?? '');
    this.differences.set(rows);
    const sector = matchSector(item.sector, refs['sectors'] ?? []);
    this.sectorMessage.set(sector.kind === 'exact' ? 'Setor com nome correspondente no catálogo. Confira antes de aplicar.'
      : sector.kind === 'alias' ? 'Equivalência de nome conhecida em português/inglês. Confira se corresponde à sua classificação.'
      : sector.kind === 'ambiguous' ? 'Mais de um setor corresponde à fonte: ' + sector.candidates.map(c => c.name).join(', ') + '. Escolha qual revisar.'
      : sector.kind === 'missing' ? 'Setor da fonte sem correspondência conhecida. Escolha um setor do catálogo para revisar ou mantenha o atual.'
      : 'A fonte não informou setor. Escolha um setor do catálogo para revisar ou mantenha o atual.');
    this.reviewSector(sector.candidates.length === 1 ? sector.candidates[0].id : 0);
  }
  checkField(field: Field, checked: boolean) { this.differences.update(rows => rows.map(row => row.field === field ? { ...row, checked } : row)); }
  reviewSector(id: number) {
    const sector = (this.references()['sectors'] ?? []).find(c => c.id === id);
    this.sectorChoice.set(sector?.id ?? 0); this.applied.set(false);
    this.differences.update(rows => [...rows.filter(row => row.field !== 'sectorId'), ...(sector ? [{ field: 'sectorId' as const, label: 'Setor', value: sector.id, suggested: sector.name, current: (this.references()['sectors'] ?? []).find(c => c.id === this.current().sectorId)?.name ?? '', checked: false, baseline: JSON.stringify(this.current().sectorId ?? null) }] : [])]);
  }
  fetchImage() {
    const item = this.selected(); if (!item || this.busy()) return; this.busy.set(true); this.imageMessage.set(''); this.error.set('');
    this.imageBaseline = JSON.stringify(this.current().image ?? null);
    this.http.get<{ status: string; message: string; data: string | null; contentType: string | null }>('/api/asset-discovery/image', { params: { market: item.market, symbol: item.symbol } }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: async result => {
      try {
        if (result.status === 'available' && result.data && result.contentType) {
          const bytes = Uint8Array.from(atob(result.data), c => c.charCodeAt(0)); const data = await normalizeAssetImage(new File([bytes], 'suggested', { type: result.contentType }), true);
          if (!this.destroyRef.destroyed) this.image.set(data);
        }
        if (!this.destroyRef.destroyed) this.imageMessage.set(result.message);
      } catch { if (!this.destroyRef.destroyed) this.imageMessage.set('Não foi possível preparar a imagem. Selecione outro arquivo.'); }
      finally { if (!this.destroyRef.destroyed) this.busy.set(false); }
    }, error: error => { this.error.set(apiError(error)); this.busy.set(false); } });
  }
  apply() {
    if (this.busy()) return;
    const sector = this.differences().find(row => row.field === 'sectorId' && row.checked);
    if (sector && !(this.references()['sectors'] ?? []).some(c => c.id === sector.value && c.name === sector.suggested)) {
      this.reviewSector(0); this.error.set('O catálogo de setores mudou. Escolha e confira o setor novamente.'); return;
    }
    if (this.differences().some(row => row.checked && row.baseline !== JSON.stringify(this.current()[row.field] ?? null)) || this.useImage() && this.imageBaseline !== JSON.stringify(this.current().image ?? null)) {
      const item = this.selected(), image = this.image(), message = this.imageMessage();
      if (item) this.select(item);
      this.image.set(image); this.imageMessage.set(message); this.imageBaseline = JSON.stringify(this.current().image ?? null);
      this.error.set('O formulário mudou durante a revisão. Confira novamente os campos e marque a imagem se desejar substituí-la.');
      return;
    }
    const fields: Partial<AssetInput> = {};
    for (const row of this.differences()) if (row.checked) Object.assign(fields, { [row.field]: row.value });
    if (this.image() && this.useImage()) fields.image = { data: this.image(), expectedVersion: this.imageVersion() };
    this.appliedFields.emit(fields); this.applied.set(true); this.error.set('');
    for (const row of this.differences()) if (row.checked) { row.baseline = JSON.stringify(row.value); row.current = row.suggested; }
    if (fields.image) this.imageBaseline = JSON.stringify(fields.image);
  }
}
