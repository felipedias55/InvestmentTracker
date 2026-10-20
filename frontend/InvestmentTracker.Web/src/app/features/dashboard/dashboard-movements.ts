import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PrivateCurrencyPipe } from '../../core/value-privacy';
import type { Movement } from '../movements/movements-page';
import { apiError } from '../../core/services/api-error';

@Component({
  selector: 'app-dashboard-movements', standalone: true,
  imports: [DatePipe, RouterLink, PrivateCurrencyPipe],
  template: `
    @if (loading()) { <p role="status">Carregando movimentações…</p> }
    @if (error()) { <p role="alert" class="notice error">{{ error() }}</p><button (click)="refresh()">Tentar novamente</button> }
    @if (!loading() && !error()) {
      <p class="hint">Últimos cinco registros por data da operação. Estornos e correções permanecem identificados.</p>
      @for (m of recent(); track m.id) {
        <article>
          <strong>#{{ m.id }} · {{ labels[m.kind] || m.kind }}</strong>
          <span>{{ m.date | date:'dd/MM/yyyy':'UTC' }}</span>
          <p>{{ names(m) }}</p>
          @if (financialKinds.includes(m.kind)) { <strong>{{ m.amount | currency:m.currencyCode:'code':'1.2-4' }}</strong> }
          @if (m.reversalOfId) { <small>Estorno de #{{ m.reversalOfId }}</small> }
          @if (m.reversedById) { <small>Estornado por #{{ m.reversedById }}</small> }
          @if (m.replacesMovementId) { <small>Substitui #{{ m.replacesMovementId }}</small> }
        </article>
      } @empty { <p>Nenhuma movimentação registrada.</p> }
    }
    <a routerLink="/movements">Ver todas as movimentações →</a>
  `,
  styles: ['article { padding: 12px 0; border-bottom: 1px solid var(--border); } article > span { float: right; font-size: 12px; } p { overflow-wrap: anywhere; } small { display: block; color: var(--muted); }']
})
export class DashboardMovements {
  private readonly http = inject(HttpClient);
  readonly portfolioId = input.required<number>();
  readonly active = input(false);
  readonly rows = signal<Movement[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly attempt = signal(0);

  readonly financialKinds = ['buy', 'sell', 'income', 'deposit', 'withdrawal', 'transfer'];
  readonly labels: Record<string,string> = { buy: 'Compra', sell: 'Venda', income: 'Provento', deposit: 'Depósito', withdrawal: 'Retirada', transfer: 'Transferência', adjustment: 'Ajuste de saldo', reversal: 'Estorno', historical: 'Registro histórico', 'position-adjustment': 'Ajuste de posição', 'income-conversion': 'Conversão de provento', opening: 'Saldo inicial', reopen: 'Reabertura de período', split: 'Desdobramento', 'reverse-split': 'Grupamento', bonus: 'Bonificação', correction: 'Correção retroativa' };
  constructor() {
    effect(onCleanup => {
      const id = this.portfolioId(); this.attempt();
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
