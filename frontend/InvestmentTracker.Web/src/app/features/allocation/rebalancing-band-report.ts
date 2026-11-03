import { Component, computed, effect, input, signal } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PrivateCurrencyPipe } from '../../core/value-privacy';
import { formatBrazilianNumber } from '../../core/brazilian-number';
import { ListPaginator } from '../../shared/list-paginator';
import { RebalancingBandStatus } from './allocation.service';

@Component({ selector: 'app-rebalancing-band-report', standalone: true,
  imports: [MatFormFieldModule, MatInputModule, MatSelectModule, PrivateCurrencyPipe, ListPaginator],
  template: `<section class="panel band-result"><h2>Faixas de rebalanceamento</h2>
    <p>Faixas do restante por metas na base elegível, antes de simular as reservas. Categorias e setores representam o mesmo dinheiro: não some as duas análises. Reservas explícitas podem receber valores mesmo dentro ou acima da faixa.</p>
    <div class="filters"><mat-form-field appearance="outline"><mat-label>Buscar diagnóstico de faixa</mat-label><input matInput type="search" [value]="query()" (input)="query.set($any($event.target).value); page.set(0)" /></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Dimensão das faixas</mat-label><select matNativeControl [value]="dimension()" (change)="dimension.set($any($event.target).value); page.set(0)"><option value="">Todas</option><option value="category">Categorias</option><option value="sector">Setores</option></select></mat-form-field></div>
    @for (b of pageRows(); track b.dimension + ':' + b.groupId) {
      <article><h3>{{ b.dimension === 'category' ? 'Categoria' : 'Setor' }} · {{ b.name }} · {{ labels[b.state] }}</h3>
        <p>Tolerância: {{ format(b.tolerancePoints) }} p.p. ({{ b.hasOverride ? 'específica' : 'geral' }}).</p>
        @if (b.currentPercentage !== null) { <p>Atual: {{ format(b.currentPercentage) }}% @if (b.targetPercentage !== null) { · Meta normalizada: {{ format(b.targetPercentage) }}% · Faixa: {{ format(b.lowerPercentage!) }}% a {{ format(b.upperPercentage!) }}% }.</p> }
        <p>{{ b.reason }}</p>
        <p>Restante no plano conjunto: {{ b.requestedRemainder | currency:currency():'code' }} → Após limites: {{ b.suggestedRemainder | currency:currency():'code' }} · Reservas no plano final: {{ b.reservedContribution | currency:currency():'code' }}.</p>
      </article>
    } @empty { <p>Nenhum diagnóstico corresponde aos filtros.</p> }
    <app-list-paginator [hidePageSize]="true" [length]="filtered().length" [index]="currentPage()" [size]="10" (changed)="page.set($event.pageIndex)" />
  </section>`,
  styles: [':host { display:block; min-width:0; margin-block:16px; } .filters { display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); gap:12px; } mat-form-field { width:100%; } article { border-top:1px solid var(--border); padding-block:12px; overflow-wrap:anywhere; }']
})
export class RebalancingBandReport {
  readonly rows = input<RebalancingBandStatus[]>([]); readonly currency = input.required<string>();
  readonly query = signal(''); readonly dimension = signal(''); readonly page = signal(0); readonly format = formatBrazilianNumber;
  readonly labels: Record<string, string> = { below: 'Abaixo da faixa', within: 'Dentro da faixa', above: 'Acima da faixa', excluded: 'Excluída do restante', 'no-eligible': 'Sem posições elegíveis', 'no-target': 'Sem meta válida' };
  readonly filtered = computed(() => { const normalize = (v: string) => v.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    return this.rows().filter(b => (!this.dimension() || b.dimension === this.dimension()) && normalize(b.name).includes(normalize(this.query().trim()))); });
  readonly currentPage = computed(() => Math.min(this.page(), Math.max(0, Math.ceil(this.filtered().length / 10) - 1)));
  readonly pageRows = computed(() => this.filtered().slice(this.currentPage() * 10, (this.currentPage() + 1) * 10));
  constructor() { effect(() => { this.rows(); this.query.set(''); this.dimension.set(''); this.page.set(0); }); }
}
