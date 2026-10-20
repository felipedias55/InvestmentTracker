import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AllocationRow } from './allocation.service';
import { createRequestId } from '../../core/request-id';
import { apiError } from '../../core/services/api-error';
import { parseBrazilianNumber, formatBrazilianNumber } from '../../core/brazilian-number';

export interface StrategyConfig { name: string; model: string; remainder: string; excludedCategoryIds: number[]; rules: { categoryId: number; kind: string; value: string }[]; tolerance: string; }
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
  template: `
  <details class="panel" (toggle)="open.set($any($event.target).open)" [open]="enabled()">
    <summary>Estratégia de investimento {{ enabled() ? '· ' + name() : '· metas atuais' }}</summary>
    @if (error()) { <p role="alert" class="notice error">{{ error() }}</p> }
    @if (notice()) { <p role="status">{{ notice() }}</p> }
    <fieldset [disabled]="saving() || loading()">
      <label><input type="checkbox" [checked]="enabled()" (change)="enabled.set($any($event.target).checked)" /> Usar estratégia nesta simulação</label>
      @if (enabled()) {
        <label>Configuração salva<select (change)="choose($any($event.target).value)" [value]="profileId()"><option value="">Nova configuração</option>@for (s of saved(); track s.profileId) { <option [value]="s.profileId" [selected]="profileId() === s.profileId">{{ s.config.name }} · revisão {{ s.revision }}</option> }</select></label>
        <label>Modelo<select aria-label="Modelo" [value]="model()" (change)="preset($any($event.target).value)">@for (m of models; track m.id) { <option [value]="m.id">{{ m.name }}</option> }</select></label>
        <p class="hint">{{ help() }}</p>
        <label>Nome<input maxlength="100" [value]="name()" (input)="name.set($any($event.target).value)" /></label>
        <label>Distribuir o restante<select [value]="remainder()" (change)="remainder.set($any($event.target).value)"><option value="targets">Metas de categoria e setor</option><option value="equal">Igual entre combinações de categoria/setor</option><option value="reserve">Manter sem distribuição</option></select></label>
        @if (model() === 'bands') { <label>Tolerância em pontos percentuais<input inputmode="decimal" [value]="tolerance()" (input)="tolerance.set($any($event.target).value)" /></label> }
        <p class="hint">Reservas fixas em {{ currency() }}; percentuais sobre o aporte total. Excluir remove a categoria da base do restante, não da carteira. Reservas são divididas igualmente entre os setores existentes da categoria.</p>
        @for (c of categories(); track c.groupId) {
          <div class="rule">
            <strong>{{ c.name }}</strong>
            <label><input type="checkbox" [checked]="excluded().includes(c.groupId)" (change)="exclude(c.groupId, $any($event.target).checked)" /> Excluir da base do restante</label>
            <label>Reserva<select [attr.aria-label]="'Regra de ' + c.name" [value]="rules()[c.groupId]?.kind || ''" (change)="rule(c.groupId, $any($event.target).value, rules()[c.groupId]?.value || '0')"><option value="">Sem reserva</option><option value="fixed">Valor fixo</option><option value="percent">Percentual do aporte</option></select></label>
            @if (rules()[c.groupId]; as r) { <label>{{ r.kind === 'fixed' ? currency() : '%' }}<input inputmode="decimal" [attr.aria-label]="'Valor da regra de ' + c.name" [value]="r.value" (input)="rule(c.groupId, r.kind, $any($event.target).value)" /></label> }
          </div>
        }
        @if (!valid()) { <p class="notice">Informe nome e valores válidos, positivos ou zero, com até duas casas decimais. Confira os percentuais.</p> }
        <button type="button" [disabled]="!valid()" (click)="save()">Salvar como estratégia padrão</button>
        <p class="hint">A última configuração salva é a padrão ao abrir esta carteira. Salvar cria uma revisão; simular usa os campos atuais sem gravá-los.</p>
      }
    </fieldset>
  </details>
  `,
  styles: ['details { display:block; margin:16px 0; } summary { cursor:pointer; } .rule { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:12px; align-items:center; border-top:1px solid var(--border); padding:12px 0; } input[type=checkbox] { width:auto; } label { display:block; }']
})
export class StrategyEditor {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  readonly portfolioId = input.required<number>(); readonly currency = input.required<string>(); readonly categories = input<AllocationRow[]>([]);
  readonly changed = output<{ config: StrategyConfig | null; valid: boolean }>();
  readonly models = strategyModels; readonly enabled = signal(false); readonly open = signal(false);
  readonly loading = signal(false); readonly saving = signal(false); readonly error = signal(''); readonly notice = signal('');
  readonly saved = signal<SavedStrategy[]>([]); readonly profileId = signal(''); readonly revision = signal(0);
  readonly name = signal('Alocação por metas'); readonly model = signal('targets'); readonly remainder = signal('targets');
  readonly tolerance = signal('5'); readonly excluded = signal<number[]>([]);
  readonly rules = signal<Record<number, { kind: string; value: string }>>({});
  readonly help = computed(() => this.models.find(m => m.id === this.model())?.help ?? '');
  readonly valid = computed(() => {
    const values = Object.values(this.rules());
    return !!this.name().trim() && this.name().length <= 100 && values.every(r => /^\d+(?:\.\d{3})*(?:,\d{1,2})?$/.test(r.value) && Number(parseBrazilianNumber(r.value)) <= (r.kind === 'percent' ? 100 : 999999999999999.99))
      && values.filter(r => r.kind === 'percent').reduce((s,r) => s + Number(parseBrazilianNumber(r.value)),0) <= 100
      && (this.model() !== 'real-estate' || Object.entries(this.rules()).some(([id,r]) => r.kind === 'fixed' && this.excluded().includes(Number(id))))
      && (this.model() !== 'core-satellite' || values.some(r => r.kind === 'percent'))
      && /^\d+(?:,\d{1,2})?$/.test(this.tolerance()) && Number(parseBrazilianNumber(this.tolerance())) <= 100;
  });
  readonly config = computed<StrategyConfig>(() => ({ name: this.name().trim(), model: this.model(), remainder: this.remainder(), excludedCategoryIds: this.excluded(),
    rules: Object.entries(this.rules()).map(([id,r]) => ({ categoryId: Number(id), kind: r.kind, value: parseBrazilianNumber(r.value) })),
    tolerance: (Number(parseBrazilianNumber(this.tolerance())) / 100).toFixed(4) }));
  constructor() {
    effect(() => this.changed.emit({ config: this.enabled() ? this.config() : null, valid: !this.loading() && !this.saving() && (!this.enabled() || this.valid()) }));
    effect(onCleanup => {
      const id = this.portfolioId(); this.saving.set(false); this.enabled.set(false); this.saved.set([]); this.profileId.set(''); this.revision.set(0);
      this.rules.set({}); this.excluded.set([]); this.error.set(''); this.notice.set(''); this.loading.set(true);
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
  }
  choose(id: string) {
    const s = this.saved().find(s => s.profileId === id);
    if (!s) { this.preset('custom'); return; }
    this.profileId.set(id); this.revision.set(s.revision); this.model.set(s.config.model); this.name.set(s.config.name); this.remainder.set(s.config.remainder);
    this.excluded.set(s.config.excludedCategoryIds); this.tolerance.set(formatBrazilianNumber((Number(s.config.tolerance) * 100).toFixed(2)));
    this.rules.set(Object.fromEntries(s.config.rules.map(r => [r.categoryId, { kind: r.kind, value: formatBrazilianNumber(r.value) }])));
    this.enabled.set(true);
  }
  exclude(id: number, checked: boolean) { this.excluded.update(ids => checked ? [...new Set([...ids,id])] : ids.filter(i => i !== id)); }
  rule(id: number, kind: string, value: string) { this.rules.update(rows => { const next = { ...rows }; if (kind) next[id] = { kind, value }; else delete next[id]; return next; }); }
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
