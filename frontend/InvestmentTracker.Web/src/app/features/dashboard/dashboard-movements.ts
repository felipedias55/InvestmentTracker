import { MatButtonModule } from '@angular/material/button';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PrivateCurrencyPipe } from '../../core/value-privacy';
import type { Movement } from '../movements/movements-page';
import { apiError } from '../../core/services/api-error';
import { EditPanel } from '../../shared/edit-panel';
import { formatBrazilianNumber } from '../../core/brazilian-number';

@Component({
  selector: 'app-dashboard-movements', standalone: true,
  imports: [EditPanel, MatButtonModule, DatePipe, RouterLink, PrivateCurrencyPipe],
  template: `
    @if (loading()) { <p role="status">Carregando movimentações…</p> }
    @if (error()) { <p role="alert" class="notice error">{{ error() }}</p><button mat-stroked-button (click)="refresh()">Tentar novamente</button> }
    @if (!loading() && !error()) {
      <p class="hint">Últimos cinco registros por data. Selecione para ver os detalhes.</p>
      @for (m of recent(); track m.id) {
        <article>
          <button mat-button type="button" class="movement-open" [attr.aria-label]="'Ver detalhes do lançamento #' + m.id" (click)="selected.set(m)"><span class="movement-meta"><strong>#{{ m.id }} · {{ labels[m.kind] || m.kind }}</strong><time>{{ m.date | date:'dd/MM/yyyy':'UTC' }}</time></span><span class="movement-name" [title]="names(m) || m.description">{{ names(m) || m.description || 'Ver detalhes' }}</span></button>
          @if (financialKinds.includes(m.kind)) { <strong class="movement-amount">{{ m.amount | currency:m.currencyCode:'code':'1.2-4' }}</strong> }
          @if (m.reversalOfId) { <small>Estorno de #{{ m.reversalOfId }}</small> }
          @if (m.reversedById) { <small>Estornado por #{{ m.reversedById }}</small> }
          @if (m.replacesMovementId) { <small>Substitui #{{ m.replacesMovementId }}</small> }
        </article>
      } @empty { <p>Nenhuma movimentação registrada.</p> }
    }
    <a routerLink="/movements">Ver todas as movimentações →</a>
    @if (selected(); as m) {
      <app-edit-panel [editing]="true" [wide]="true" title="Detalhes da movimentação recente" (closed)="selected.set(null)"><div class="movement-details">
        <h2>#{{ m.id }} · {{ labels[m.kind] || m.kind }}</h2><p>{{ m.date | date:'dd/MM/yyyy':'UTC' }} · {{ m.description }}</p>
        @if (names(m)) { <p>{{ names(m) }}</p> }
        @if (financialKinds.includes(m.kind)) { <p>{{ m.amount | currency:m.currencyCode:'code':'1.2-4' }}</p> }
        <p>Registrado em {{ m.createdAtUtc | date:'dd/MM/yyyy HH:mm' }}</p>
        @if (m.reversalOfId) { <p>Estorno de #{{ m.reversalOfId }}</p> }
        @if (m.reversedById) { <p>Estornado por #{{ m.reversedById }}</p> }
        @if (m.correctionId) { <p>Parte da correção #{{ m.correctionId }}</p> }
        @if (m.replacesMovementId) { <p>Substitui #{{ m.replacesMovementId }}</p> }
        @if (m.tradeId) { <p>Operação #{{ m.tradeId }}</p> }
        @if (m.incomeReceiptId) { <p>Recebimento #{{ m.incomeReceiptId }}</p> }
        @for (e of m.effects ?? []; track $index) {
          <section class="effect"><h3>{{ e.name }}</h3><p>Saldo: {{ e.beforeValue | currency:e.currencyCode:'code':'1.2-4' }} → {{ e.afterValue | currency:e.currencyCode:'code':'1.2-4' }}</p>
          @if (e.isPosition) { <p>Quantidade: {{ formatNumber(e.beforeQuantity) }} → {{ formatNumber(e.afterQuantity) }}</p><p>Custo: {{ e.beforeCost | currency:e.currencyCode:'code':'1.2-4' }} → {{ e.afterCost | currency:e.currencyCode:'code':'1.2-4' }}</p><p>Proventos: {{ e.beforeIncome | currency:e.currencyCode:'code':'1.2-4' }} → {{ e.afterIncome | currency:e.currencyCode:'code':'1.2-4' }}</p> }</section>
        }
        <p class="hint">Consulta dos dados carregados. Consulte Movimentações para atualizar a auditoria, estornar ou corrigir.</p>
        <a mat-button routerLink="/movements">Abrir Movimentações</a>
        <div class="detail-actions"><button mat-stroked-button type="button" (click)="selected.set(null)">Fechar detalhes</button></div>
      </div></app-edit-panel>
    }
  `,
  styles: [`
    article { display: grid; grid-template-columns: minmax(0,1fr) auto; align-items: center; gap: 4px 12px; padding: 6px 0; border-bottom: 1px solid var(--border); }
    .movement-open { height: auto; min-height: 44px; min-width: 0; padding: 4px 0; justify-content: flex-start; text-align: left; white-space: normal; overflow: hidden; }
    .movement-meta { display: flex; flex-wrap: wrap; gap: 4px 10px; align-items: center; font-size: 12px; }
    time { color: var(--muted); font-weight: 400; }
    .movement-name { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 240px; font-size: 12px; font-weight: 400; }
    .movement-amount { text-align: right; font-size: 13px; overflow-wrap: anywhere; }
    p { overflow-wrap: anywhere; }
    small { grid-column: 1/-1; color: var(--muted); }
    .movement-details { padding: 16px; }
    .effect { padding-top: 8px; border-top: 1px solid var(--border); }
    .detail-actions { display: flex; justify-content: flex-end; margin-top: 16px; }
    @media(max-width: 700px) { .movement-name { max-width: 180px; } }
  `]
})
export class DashboardMovements {
  private readonly http = inject(HttpClient);
  readonly portfolioId = input.required<number>();
  readonly active = input(false);
  readonly rows = signal<Movement[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly attempt = signal(0);
  readonly selected = signal<Movement | null>(null);
  readonly formatNumber = formatBrazilianNumber;

  readonly financialKinds = ['buy', 'sell', 'income', 'deposit', 'withdrawal', 'transfer'];
  readonly labels: Record<string,string> = { buy: 'Compra', sell: 'Venda', income: 'Provento', deposit: 'Depósito', withdrawal: 'Retirada', transfer: 'Transferência', adjustment: 'Ajuste de saldo', reversal: 'Estorno', historical: 'Registro histórico', 'position-adjustment': 'Ajuste de posição', 'income-conversion': 'Conversão de provento', opening: 'Saldo inicial', reopen: 'Reabertura de período', split: 'Desdobramento', 'reverse-split': 'Grupamento', bonus: 'Bonificação', correction: 'Correção retroativa' };
  constructor() {
    effect(onCleanup => {
      const id = this.portfolioId(); this.attempt();
      this.selected.set(null);
      this.rows.set([]);
      if (!this.active()) { this.loading.set(false); return; }

      this.error.set(''); this.loading.set(true);
      const request = this.http.get<Movement[]>(`/api/portfolios/${id}/movements`).subscribe({
        next: rows => { this.rows.set(rows); this.loading.set(false); },
        error: e => { this.error.set(apiError(e)); this.loading.set(false); }
      });
      onCleanup(() => request.unsubscribe());
    });
  }
  refresh() { this.attempt.update(n => n + 1); }
  names(m: Movement) { return [...new Set((m.effects ?? []).map(e => e.name))].join(' · '); }
  readonly recent = computed(() => [...this.rows()].sort((a,b) => b.date.localeCompare(a.date) || b.createdAtUtc.localeCompare(a.createdAtUtc) || b.id - a.id).slice(0,5));
}
