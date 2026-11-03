import { DraftState } from '../../shared/draft-state';
import { EditPanel } from '../../shared/edit-panel';
import { ListPaginator } from '../../shared/list-paginator';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AllocationRow } from './allocation.service';
import { createRequestId } from '../../core/request-id';
import { apiError } from '../../core/services/api-error';
import { Position } from '../portfolio/portfolio.service';
import { parseBrazilianNumber, formatBrazilianNumber } from '../../core/brazilian-number';

export interface StrategyCombination { categoryId: number; sectorId: number; }
export interface ConcentrationLimit { dimension: string; groupId: number; maximumPercentage: string; }
export interface StrategyGroupPreference { categoryId: number; sectorId: number; priority?: number; preferredCurrency?: string | null; }
export interface StrategyConfig { name: string; model: string; remainder: string; excludedCategoryIds: number[]; rules: { categoryId: number; kind: string; value: string; priority?: number }[]; tolerance: string;
  excludedCombinations?: StrategyCombination[] | null; concentrationLimits?: ConcentrationLimit[] | null;
  usePriorities?: boolean; groupPreferences?: StrategyGroupPreference[] | null; }
interface SavedStrategy { profileId: string; revision: number; config: StrategyConfig; }
export const strategyModels = [
  { id: 'targets', name: 'Alocação por metas', remainder: 'targets', help: 'Distribui o aporte entre grupos abaixo das metas de categoria e setor.' },
  { id: 'regular', name: 'Aportes regulares', remainder: 'equal', help: 'Divide o aporte informado igualmente entre combinações elegíveis. Não agenda operações nem controla metas mensais nesta etapa.' },
  { id: 'buy-hold', name: 'Buy and hold', remainder: 'targets', help: 'Acumulação de longo prazo com novos aportes pelas metas. Não sugere vendas; a decisão de manter ativos continua sendo sua.' },
  { id: 'income', name: 'Foco em renda', remainder: 'reserve', help: 'Defina reservas para as categorias escolhidas para renda. Não escolhe ativos pelo dividend yield nem prevê dividendos.' },
  { id: 'core-satellite', name: 'Núcleo e complementos', remainder: 'reserve', help: 'Defina percentuais do aporte para categorias do núcleo e dos complementos. Percentual do aporte não é meta de patrimônio.' },
  { id: 'bands', name: 'Rebalanceamento por faixas', remainder: 'targets', help: 'Direciona novos aportes somente a desvios fora da tolerância. A faixa é em pontos percentuais; não executa vendas.' },
  { id: 'real-estate', name: 'Imobiliário fixo + metas', remainder: 'targets', help: 'Escolha explicitamente FIIs/REITs, defina uma reserva fixa para cada categoria e exclua-as da base do restante.' },
  { id: 'custom', name: 'Personalizada / híbrida', remainder: 'targets', help: 'Combine reservas, exclusões e distribuição do restante. Categorias reservadas também recebem restante se não forem excluídas.' },
];
@Component({
  selector: 'app-strategy-editor', standalone: true,
  imports: [DraftState, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule, EditPanel, ListPaginator],
  template: `
  <div [appDraftState]="config()" [draftBusy]="saving()" [draftSaved]="notice()"><section class="panel strategy-summary">
    <div><strong>Estratégia de investimento</strong><p>{{ enabled() ? name() : 'Metas atuais da carteira' }}</p></div>
    <button mat-stroked-button (click)="open.set(true)">Configurar estratégia</button>
    @if (enabled() && !valid() && !open()) { <p class="notice error">Estratégia incompleta. Revise a configuração para simular.</p> }
    @if (loading()) { <p role="status">Carregando estratégia…</p> }
    @if (error() && !open()) { <p role="alert" class="notice error">{{ error() }}</p> }
  </section>
  @if (open()) {
  <app-edit-panel [editing]="true" [busy]="saving()" [wide]="true" title="Configurar estratégia" (closed)="open.set(false)"><section class="panel">
    @if (error()) { <p role="alert" class="notice error">{{ error() }}</p> }
    @if (notice()) { <p role="status">{{ notice() }}</p> }
    <fieldset [disabled]="saving() || loading()">
      <mat-checkbox [checked]="enabled()" (change)="enabled.set($event.checked)">Usar estratégia nesta simulação</mat-checkbox>
      @if (enabled()) {
        <mat-form-field appearance="outline"><mat-label>Configuração salva</mat-label><select matNativeControl (change)="choose($any($event.target).value)" [value]="profileId()"><option value="">Nova configuração</option>@for (s of saved(); track s.profileId) { <option [value]="s.profileId" [selected]="profileId() === s.profileId">{{ s.config.name }} · revisão {{ s.revision }}</option> }</select></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Modelo</mat-label><select matNativeControl aria-label="Modelo" [value]="model()" (change)="preset($any($event.target).value)">@for (m of models; track m.id) { <option [value]="m.id">{{ m.name }}</option> }</select></mat-form-field>
        <details><summary>Como funciona este modelo</summary><p>{{ help() }}</p></details>
        <mat-form-field appearance="outline"><mat-label>Nome</mat-label><input matInput maxlength="100" [value]="name()" (input)="name.set($any($event.target).value)" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Distribuir o restante</mat-label><select matNativeControl [value]="remainder()" (change)="remainder.set($any($event.target).value)"><option value="targets">Metas de categoria e setor</option><option value="equal">Igual entre combinações de categoria/setor</option><option value="reserve">Manter sem distribuição</option></select></mat-form-field>
        @if (model() === 'bands') { <mat-form-field appearance="outline"><mat-label>Tolerância em pontos percentuais</mat-label><input matInput inputmode="decimal" [value]="tolerance()" (input)="tolerance.set($any($event.target).value)" /></mat-form-field> }
        <p class="hint">Reservas fixas em {{ currency() }}; percentuais sobre o aporte total. Excluir remove a categoria da base do restante, não da carteira. Reservas são divididas igualmente entre os setores existentes da categoria.</p>
        <mat-checkbox [checked]="usePriorities()" (change)="usePriorities.set($event.checked)">Atender regras por prioridade</mat-checkbox>
        <p class="hint">Prioridade de 1 a 1000: números menores primeiro; 100 é o padrão. Reservas vêm antes do restante. Empates no orçamento são proporcionais à demanda. Limites e elegibilidade prevalecem.</p>
        <mat-form-field appearance="outline"><mat-label>Buscar categoria nas regras</mat-label><input matInput type="search" [value]="query()" (input)="query.set($any($event.target).value); pageIndex.set(0)" /></mat-form-field>
        @if (!filteredCategories().length) { <p>Nenhuma categoria corresponde à busca.</p> }
        @for (c of pageCategories(); track c.groupId) {
          <div class="rule">
            <strong>{{ c.name }}</strong>
            <mat-checkbox [checked]="excluded().includes(c.groupId)" (change)="exclude(c.groupId, $event.checked)">Excluir da base do restante</mat-checkbox>
            <mat-form-field appearance="outline"><mat-label>Reserva</mat-label><select matNativeControl [attr.aria-label]="'Regra de ' + c.name" [value]="rules()[c.groupId]?.kind || ''" (change)="rule(c.groupId, $any($event.target).value, rules()[c.groupId]?.value || '0')"><option value="">Sem reserva</option><option value="fixed">Valor fixo</option><option value="percent">Percentual do aporte</option></select></mat-form-field>
            @if (rules()[c.groupId]; as r) { <mat-form-field appearance="outline"><mat-label>{{ r.kind === 'fixed' ? currency() : '%' }}</mat-label><input matInput inputmode="decimal" [attr.aria-label]="'Valor da regra de ' + c.name" [value]="r.value" (input)="rule(c.groupId, r.kind, $any($event.target).value)" /></mat-form-field> }
            @if (usePriorities() && rules()[c.groupId]) {
              <mat-form-field appearance="outline"><mat-label>Prioridade da reserva</mat-label><input matInput inputmode="numeric" [attr.aria-label]="'Prioridade da reserva de ' + c.name"
                [value]="rules()[c.groupId]?.priority || '100'" (input)="rulePriority(c.groupId, $any($event.target).value)" /></mat-form-field>
            }
          </div>
        }
        <mat-form-field appearance="outline"><mat-label>Regras por página</mat-label><select matNativeControl [value]="pageSize()" (change)="pageSize.set(+$any($event.target).value); pageIndex.set(0)"><option value="10">10</option><option value="25">25</option><option value="50">50</option></select></mat-form-field>
        <app-list-paginator [hidePageSize]="true" [length]="filteredCategories().length" [index]="currentPage()" [size]="pageSize()" (changed)="pageIndex.set($event.pageIndex); pageSize.set($event.pageSize)" />
        <details class="combination-rules"><summary>Elegibilidade por categoria e setor · bloqueios: {{ blockedCombinations().length }}</summary>
          <p class="hint">Bloquear uma combinação impede reservas e restante para ela e a retira da base elegível. Excluir uma categoria acima afeta apenas o restante. Nada remove ativos do patrimônio. A classificação FII/REIT e o setor imobiliário são definidos em Tipos de ativo e Setores.</p>
          <mat-form-field appearance="outline"><mat-label>Buscar combinação</mat-label><input matInput type="search" [value]="combinationQuery()" (input)="combinationQuery.set($any($event.target).value); combinationPage.set(0)" /></mat-form-field>
          @for (g of pageCombinations(); track g.key) {
            <div class="combination-rule"><span>{{ g.category }} / {{ g.sector }}</span>
              <mat-checkbox [aria-label]="'Bloquear ' + g.category + ' / ' + g.sector" [checked]="isBlocked(g)" (change)="block(g, $event.checked)">Bloquear aportes</mat-checkbox>
              @if (usePriorities()) {
                <mat-form-field appearance="outline"><mat-label>Prioridade do grupo</mat-label><input matInput inputmode="numeric" [attr.aria-label]="'Prioridade de ' + g.category + ' / ' + g.sector"
                  [value]="preference(g).priority" (input)="prefer(g, 'priority', $any($event.target).value)" /></mat-form-field>
              }
              <mat-form-field appearance="outline"><mat-label>Moeda preferida neste grupo</mat-label><select matNativeControl [attr.aria-label]="'Moeda preferida de ' + g.category + ' / ' + g.sector"
                [value]="preference(g).currency" (change)="prefer(g, 'currency', $any($event.target).value)"><option value="">Sem preferência</option><option value="BRL">Real (BRL)</option><option value="USD">Dólar (USD)</option></select></mat-form-field>
            </div>
          } @empty { <p>Nenhuma combinação corresponde à busca.</p> }
          <app-list-paginator [hidePageSize]="true" [length]="filteredCombinations().length" [index]="currentCombinationPage()" [size]="10" (changed)="combinationPage.set($event.pageIndex)" />
        </details>
        <details class="concentration-limits"><summary>Limites de concentração · configurados: {{ limitCount() }}</summary>
          <p class="hint">Teto sobre todas as posições da carteira mais somente o aporte distribuído, sem Patrimônio Externo. Pode reduzir reservas e metas. Vazio significa sem limite; 0% impede novos aportes no grupo, mas não retira suas posições da base.</p>
          <mat-form-field appearance="outline"><mat-label>Buscar grupo nos limites</mat-label><input matInput type="search" [value]="limitQuery()" (input)="limitQuery.set($any($event.target).value); limitPage.set(0)" /></mat-form-field>
          @for (g of pageLimits(); track g.key) {
            <div class="limit-rule"><strong>{{ g.dimension === 'category' ? 'Categoria' : 'Setor' }} · {{ g.name }}</strong>
              <mat-form-field appearance="outline"><mat-label>Concentração máxima (%)</mat-label><input matInput inputmode="decimal" placeholder="Sem limite"
                [attr.aria-label]="'Teto de ' + (g.dimension === 'category' ? 'categoria ' : 'setor ') + g.name"
                [value]="limits()[g.key] || ''" (input)="limit(g.dimension, g.groupId, $any($event.target).value)" /></mat-form-field>
            </div>
          } @empty { <p>Nenhum grupo corresponde à busca.</p> }
          <app-list-paginator [hidePageSize]="true" [length]="filteredLimits().length" [index]="currentLimitPage()" [size]="10" (changed)="limitPage.set($event.pageIndex)" />
        </details>
        @if (!valid()) { <p class="notice">Confira nome, valores e percentuais. Limites: 0% a 100% (até 200); prioridades: inteiros de 1 a 1000; até 500 preferências de grupo.</p> }
        <button mat-flat-button type="button" [disabled]="!valid()" (click)="save()">Salvar como estratégia padrão</button>
        <p class="hint">A última configuração salva é a padrão ao abrir esta carteira. Salvar cria uma revisão; simular usa os campos atuais sem gravá-los.</p>
      }
    <div class="actions"><button mat-stroked-button type="button" (click)="open.set(false)" [disabled]="saving()">Concluir configuração</button></div>
    </fieldset>
  </section></app-edit-panel>
  }
  </div>`,
  styles: ['mat-form-field { width:100%; } .strategy-summary { display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:12px; } fieldset { min-width:0; } details { display:block; margin:16px 0; } summary { cursor:pointer; } .rule, .limit-rule { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:12px; align-items:center; border-top:1px solid var(--border); padding:12px 0; } .combination-rule { display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); align-items:center; gap:12px; padding:12px 0; border-top:1px solid var(--border); overflow-wrap:anywhere; } .limit-rule { overflow-wrap:anywhere; } input[type=checkbox] { width:auto; } label { display:block; }']
})
export class StrategyEditor {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  readonly portfolioId = input.required<number>(); readonly currency = input.required<string>(); readonly categories = input<AllocationRow[]>([]);
  readonly sectors = input<AllocationRow[]>([]); readonly positions = input<Position[]>([]);
  readonly changed = output<{ config: StrategyConfig | null; valid: boolean }>();
  readonly models = strategyModels; readonly enabled = signal(false); readonly open = signal(false);
  readonly loading = signal(false); readonly saving = signal(false); readonly error = signal(''); readonly notice = signal('');
  readonly saved = signal<SavedStrategy[]>([]); readonly profileId = signal(''); readonly revision = signal(0);
  readonly name = signal('Alocação por metas'); readonly model = signal('targets'); readonly remainder = signal('targets');
  readonly tolerance = signal('5'); readonly excluded = signal<number[]>([]);
  readonly rules = signal<Record<number, { kind: string; value: string; priority?: string }>>({});
  readonly usePriorities = signal(false);
  readonly groupPreferences = signal<Record<string, { priority: string; currency: string }>>({});
  readonly blockedCombinations = signal<StrategyCombination[]>([]);
  readonly limits = signal<Record<string, string>>({});
  readonly limitCount = computed(() => Object.keys(this.limits()).length);
  readonly limitQuery = signal(''); readonly limitPage = signal(0);
  readonly limitGroups = computed(() => {
    const rows = [...this.categories().map(c => ({ dimension: 'category', groupId: c.groupId, name: c.name })),
      ...this.sectors().map(s => ({ dimension: 'sector', groupId: s.groupId, name: s.name }))];
    for (const key of Object.keys(this.limits())) {
      const [dimension, id] = key.split(':');
      if (!rows.some(r => r.dimension === dimension && r.groupId === Number(id))) rows.push({ dimension, groupId: Number(id), name: 'Grupo indisponível #' + id });
    }
    return rows.map(g => ({ ...g, key: `${g.dimension}:${g.groupId}` }));
  });
  readonly filteredLimits = computed(() => {
    const normalize = (v: string) => v.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    const query = normalize(this.limitQuery().trim());
    return this.limitGroups().filter(g => normalize((g.dimension === 'category' ? 'Categoria ' : 'Setor ') + g.name).includes(query));
  });
  readonly currentLimitPage = computed(() => Math.min(this.limitPage(), Math.max(0, Math.ceil(this.filteredLimits().length / 10) - 1)));
  readonly pageLimits = computed(() => this.filteredLimits().slice(this.currentLimitPage() * 10, (this.currentLimitPage() + 1) * 10));
  readonly combinationQuery = signal(''); readonly combinationPage = signal(0);
  readonly combinations = computed(() => {
    const groups = new Map<string, StrategyCombination>();
    for (const p of this.positions()) if (p.assetCategoryId && p.sectorId) groups.set(`${p.assetCategoryId}-${p.sectorId}`, { categoryId: p.assetCategoryId, sectorId: p.sectorId });
    for (const g of this.blockedCombinations()) groups.set(`${g.categoryId}-${g.sectorId}`, g);
    for (const key of Object.keys(this.groupPreferences())) { const [categoryId, sectorId] = key.split('-').map(Number); groups.set(key, { categoryId, sectorId }); }
    return [...groups.values()].map(g => ({ ...g, key: `${g.categoryId}-${g.sectorId}`,
      category: this.categories().find(c => c.groupId === g.categoryId)?.name ?? `Categoria indisponível #${g.categoryId}`,
      sector: this.sectors().find(s => s.groupId === g.sectorId)?.name ?? `Setor indisponível #${g.sectorId}` }))
      .sort((a, b) => a.category.localeCompare(b.category, 'pt-BR') || a.sector.localeCompare(b.sector, 'pt-BR'));
  });
  readonly filteredCombinations = computed(() => {
    const normalize = (v: string) => v.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    const query = normalize(this.combinationQuery().trim());
    return this.combinations().filter(g => normalize(g.category + ' ' + g.sector).includes(query));
  });
  readonly currentCombinationPage = computed(() => Math.min(this.combinationPage(), Math.max(0, Math.ceil(this.filteredCombinations().length / 10) - 1)));
  readonly pageCombinations = computed(() => this.filteredCombinations().slice(this.currentCombinationPage() * 10, (this.currentCombinationPage() + 1) * 10));
  readonly query = signal(''); readonly pageIndex = signal(0); readonly pageSize = signal(10);
  readonly filteredCategories = computed(() => {
    const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    return this.categories().filter(c => normalize(c.name).includes(normalize(this.query().trim())));
  });
  readonly currentPage = computed(() => Math.min(this.pageIndex(), Math.max(0, Math.ceil(this.filteredCategories().length / this.pageSize()) - 1)));
  readonly pageCategories = computed(() => this.filteredCategories().slice(this.currentPage() * this.pageSize(), (this.currentPage() + 1) * this.pageSize()));
  readonly help = computed(() => this.models.find(m => m.id === this.model())?.help ?? '');
  readonly valid = computed(() => {
    const values = Object.values(this.rules());
    const priority = (v: string) => /^\d+$/.test(v) && Number(v) >= 1 && Number(v) <= 1000;
    return values.every(r => priority(r.priority ?? '100')) && Object.keys(this.groupPreferences()).length <= 500
      && Object.values(this.groupPreferences()).every(p => priority(p.priority) && ['', 'BRL', 'USD'].includes(p.currency))
      && this.limitCount() <= 200 && Object.values(this.limits()).every(v => /^\d+(?:,\d{1,2})?$/.test(v) && Number(parseBrazilianNumber(v)) <= 100)
      && !!this.name().trim() && this.name().length <= 100 && values.every(r => {
      if (!/^\d+(?:\.\d{3})*(?:,\d{1,2})?$/.test(r.value)) return false;
      const value = parseBrazilianNumber(r.value);
      // Keep fixed amounts as decimal strings: the backend maximum is not exactly representable as a JS number.
      return r.kind === 'percent' ? Number(value) <= 100 : value.split('.')[0].replace(/^0+/, '').length <= 15;
    })
      && values.filter(r => r.kind === 'percent').reduce((s,r) => s + Number(parseBrazilianNumber(r.value)),0) <= 100
      && (this.model() !== 'real-estate' || Object.entries(this.rules()).some(([id,r]) => r.kind === 'fixed' && this.excluded().includes(Number(id))))
      && (this.model() !== 'core-satellite' || values.some(r => r.kind === 'percent'))
      && /^\d+(?:,\d{1,2})?$/.test(this.tolerance()) && Number(parseBrazilianNumber(this.tolerance())) <= 100;
  });
  readonly config = computed<StrategyConfig>(() => ({ name: this.name().trim(), model: this.model(), remainder: this.remainder(), excludedCategoryIds: this.excluded(),
    rules: Object.entries(this.rules()).map(([id,r]) => ({ categoryId: Number(id), kind: r.kind, value: parseBrazilianNumber(r.value), priority: Number(r.priority ?? '100') })),
    tolerance: (Number(parseBrazilianNumber(this.tolerance())) / 100).toFixed(4), excludedCombinations: this.blockedCombinations(),
    concentrationLimits: Object.entries(this.limits()).map(([key, value]) => { const [dimension, id] = key.split(':');
      return { dimension, groupId: Number(id), maximumPercentage: parseBrazilianNumber(value) }; }),
    usePriorities: this.usePriorities(), groupPreferences: Object.entries(this.groupPreferences()).map(([key, p]) => {
      const [categoryId, sectorId] = key.split('-').map(Number); return { categoryId, sectorId, priority: Number(p.priority), preferredCurrency: p.currency || null }; }) }));
  constructor() {
    effect(() => this.changed.emit({ config: this.enabled() ? this.config() : null, valid: !this.loading() && !this.saving() && (!this.enabled() || this.valid()) }));
    effect(onCleanup => {
      const id = this.portfolioId(); this.open.set(false); this.query.set(''); this.pageIndex.set(0); this.saving.set(false); this.enabled.set(false); this.saved.set([]); this.profileId.set(''); this.revision.set(0);
      this.rules.set({}); this.excluded.set([]); this.error.set(''); this.notice.set(''); this.loading.set(true);
      this.blockedCombinations.set([]); this.combinationQuery.set(''); this.combinationPage.set(0);
      this.limits.set({}); this.limitQuery.set(''); this.limitPage.set(0);
      this.usePriorities.set(false); this.groupPreferences.set({});
      const sub = this.http.get<SavedStrategy[]>(`/api/portfolios/${id}/strategies`).subscribe({
        next: rows => { this.saved.set(rows); if (rows.length) this.choose(rows[0].profileId); this.loading.set(false); },
        error: e => { this.error.set(apiError(e)); this.loading.set(false); }
      }); onCleanup(() => sub.unsubscribe());
    });
  }
  preset(id: string) {
    const m = this.models.find(m => m.id === id)!;
    this.profileId.set(''); this.revision.set(0); this.model.set(id); this.name.set(m.name); this.remainder.set(m.remainder);
    this.rules.set({}); this.excluded.set([]); this.notice.set('');
    this.blockedCombinations.set([]); this.combinationQuery.set(''); this.combinationPage.set(0);
    this.limits.set({}); this.limitQuery.set(''); this.limitPage.set(0);
    this.usePriorities.set(false); this.groupPreferences.set({});
  }
  choose(id: string) {
    const s = this.saved().find(s => s.profileId === id);
    if (!s) { this.preset('custom'); return; }
    this.profileId.set(id); this.revision.set(s.revision); this.model.set(s.config.model); this.name.set(s.config.name); this.remainder.set(s.config.remainder);
    this.excluded.set(s.config.excludedCategoryIds); this.tolerance.set(formatBrazilianNumber((Number(s.config.tolerance) * 100).toFixed(2)));
    this.blockedCombinations.set(s.config.excludedCombinations ?? []);
    this.limits.set(Object.fromEntries((s.config.concentrationLimits ?? []).map(l => [`${l.dimension}:${l.groupId}`, formatBrazilianNumber(l.maximumPercentage)])));
    this.rules.set(Object.fromEntries(s.config.rules.map(r => [r.categoryId, { kind: r.kind, value: formatBrazilianNumber(r.value), priority: String(r.priority ?? 100) }])));
    this.usePriorities.set(s.config.usePriorities ?? false);
    this.groupPreferences.set(Object.fromEntries((s.config.groupPreferences ?? []).map(p => [`${p.categoryId}-${p.sectorId}`, { priority: String(p.priority ?? 100), currency: p.preferredCurrency ?? '' }])));
    this.enabled.set(true);
  }
  exclude(id: number, checked: boolean) { this.excluded.update(ids => checked ? [...new Set([...ids,id])] : ids.filter(i => i !== id)); }
  limit(dimension: string, id: number, value: string) {
    this.limits.update(rows => { const next = { ...rows }, key = `${dimension}:${id}`;
      if (value.trim()) next[key] = value; else delete next[key]; return next; });
  }
  isBlocked(g: StrategyCombination) { return this.blockedCombinations().some(v => v.categoryId === g.categoryId && v.sectorId === g.sectorId); }
  block(g: StrategyCombination, blocked: boolean) {
    this.blockedCombinations.update(rows => [...rows.filter(v => v.categoryId !== g.categoryId || v.sectorId !== g.sectorId),
      ...(blocked ? [{ categoryId: g.categoryId, sectorId: g.sectorId }] : [])]);
  }
  rule(id: number, kind: string, value: string) { this.rules.update(rows => { const next = { ...rows }; if (kind) next[id] = { kind, value, priority: rows[id]?.priority ?? '100' }; else delete next[id]; return next; }); }
  rulePriority(id: number, priority: string) { this.rules.update(rows => rows[id] ? { ...rows, [id]: { ...rows[id], priority } } : rows); }
  preference(g: StrategyCombination) { return this.groupPreferences()[`${g.categoryId}-${g.sectorId}`] ?? { priority: '100', currency: '' }; }
  prefer(g: StrategyCombination, field: 'priority' | 'currency', value: string) {
    this.groupPreferences.update(rows => { const next = { ...rows }, key = `${g.categoryId}-${g.sectorId}`;
      const p = { ...this.preference(g), [field]: value }; if (p.priority === '100' && !p.currency) delete next[key]; else next[key] = p; return next; });
  }
  save() {
    if (!this.valid() || this.saving()) return;
    const portfolioId = this.portfolioId();
    if (!this.profileId()) this.profileId.set(createRequestId());
    this.saving.set(true); this.error.set(''); this.notice.set('');
    this.http.post<SavedStrategy>(`/api/portfolios/${this.portfolioId()}/strategies`, { profileId: this.profileId() || createRequestId(), expectedRevision: this.revision(), config: this.config() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: row => { if (portfolioId !== this.portfolioId()) return; this.saved.update(rows => [row, ...rows.filter(s => s.profileId !== row.profileId)]); this.choose(row.profileId); this.notice.set('Estratégia salva. Posições e movimentos não foram alterados.'); this.saving.set(false); },
      error: e => { if (portfolioId !== this.portfolioId()) return; this.error.set(apiError(e)); this.saving.set(false); }
    });
  }
}
