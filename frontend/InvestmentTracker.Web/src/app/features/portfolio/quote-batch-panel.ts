import { Component, computed, inject, input, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormControl } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';
import { EditPanel } from '../../shared/edit-panel';
import { PrivateCurrencyPipe } from '../../core/value-privacy';
import { brazilianNumberValidator, formatBrazilianNumber, parseBrazilianNumber } from '../../core/brazilian-number';
import { createRequestId } from '../../core/request-id';
import { apiError } from '../../core/services/api-error';
import { Position } from './portfolio.service';

// Quantities have six decimals, prices four. Round the total to four with banker's rounding, as in C# decimal.
export function quoteTotal(quantity: string, unitPrice: string): string {
  const scaled = (value: string, decimals: number) => {
    const [whole, fraction = ''] = value.split('.');
    return BigInt(whole) * 10n ** BigInt(decimals) + BigInt(fraction.padEnd(decimals, '0'));
  };
  const product = scaled(quantity, 6) * scaled(unitPrice, 4);
  let result = product / 1000000n;
  const remainder = product % 1000000n;
  if (remainder > 500000n || remainder === 500000n && result % 2n !== 0n) result++;
  if (result > 9999999999999999999n) throw new Error('O total excede o limite da posição.');
  return `${result / 10000n}.${String(result % 10000n).padStart(4, '0')}`;
}

@Component({
  selector: 'app-quote-batch-panel', standalone: true, imports: [EditPanel, PrivateCurrencyPipe],
  template: `
    <app-edit-panel [editing]="true" [busy]="saving()" [error]="error()" title="Atualizar cotações em lote" (closed)="cancel()">
      <section class="panel">
        <h2>{{ review() ? 'Conferir cotações' : 'Atualizar cotações' }}</h2>
        <p>Informe a cotação <strong>unitária na moeda original</strong>. Apenas os ativos filtrados com quantidade positiva estão nesta lista. Deixe em branco os que não deseja atualizar.</p>
        <p class="hint">Quantidade, custo investido e proventos serão preservados. A atualização será registrada com a data de hoje e poderá deixar a fotografia de hoje desatualizada.</p>
        <fieldset [disabled]="saving() || uncertain()">
          @for (row of review() ? changed() : rows(); track row.position.id) {
            <div class="quote-row">
              <strong>{{ row.position.ticker }} · {{ row.position.currencyCode }}</strong>
              <small>{{ row.position.name }} · Quantidade: {{ formatNumber(row.position.quantity) }}</small>
              @if (!review()) {
                <label [for]="'quote-' + row.position.id">Nova cotação de {{ row.position.ticker }}</label>
                <input [id]="'quote-' + row.position.id" inputmode="decimal" autocomplete="off" placeholder="Ex.: 123,45" [value]="prices()[row.position.id] || ''" (input)="setPrice(row.position.id, $any($event.target).value)" />
              }
              @if (row.problem) { <p class="field-error" role="alert">{{ row.problem }}</p> }
              @if (row.total !== null) {
                <dl><div><dt>Valor total anterior</dt><dd>{{ row.position.currentValue | currency:row.position.currencyCode:'code':'1.2-4' }}</dd></div>
                  <div><dt>Nova cotação unitária</dt><dd>{{ row.price | currency:row.position.currencyCode:'code':'1.2-4' }}</dd></div>
                  <div><dt>Novo valor total</dt><dd>{{ row.total | currency:row.position.currencyCode:'code':'1.2-4' }}</dd></div></dl>
              }
            </div>
          }
          @if (!rows().length) { <p>Nenhuma posição com quantidade positiva nestes filtros.</p> }
        </fieldset>
        <div class="batch-actions">
          <p role="status">{{ changed().length }} posições preenchidas · {{ positions().length }} selecionadas pelos filtros</p>
          @if (changed().length > 200) { <p class="field-error">Atualize no máximo 200 posições por lote. Use os filtros para reduzir a seleção.</p> }
          @if (uncertain()) { <p class="notice">A resposta não confirmou o resultado. Tente novamente com os mesmos dados; o identificador evita duplicar o registro.</p> }
          @if (review()) {
            <button class="primary" (click)="save()" [disabled]="saving()">{{ saving() ? 'Salvando lote…' : uncertain() ? 'Tentar novamente' : 'Confirmar atualização do lote' }}</button>
            <button (click)="review.set(false)" [disabled]="saving() || uncertain()">Voltar à edição</button>
          } @else { <button class="primary" (click)="review.set(true)" [disabled]="!valid()">Revisar {{ changed().length }} cotações</button> }
          <button (click)="cancel()" [disabled]="saving()">Cancelar</button>
          @if (confirmClose()) { <p>Fechar e descartar os campos preenchidos? Se houve falha de comunicação, confira a carteira antes de iniciar outro lote.</p><button (click)="closed.emit()">Fechar sem continuar</button><button (click)="confirmClose.set(false)">Continuar edição</button> }
        </div>
      </section>
    </app-edit-panel>
  `,
  styles: [`
    .quote-row { margin: 16px 0; padding: 16px 0; border-bottom: 1px solid var(--border); }
    small { display: block; color: var(--muted); margin-top: 6px; }
    dl { display: grid; gap: 10px; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); }
    dt { color: var(--muted); font-size: 12px; } dd { margin: 4px 0 0; font-weight: 600; overflow-wrap: anywhere; }
    .batch-actions { position: sticky; bottom: -24px; background: white; border-top: 1px solid var(--border); padding: 12px 0; }
    .batch-actions button { margin: 4px; } .batch-actions p { margin: 8px 0; }
  `],
})
export class QuoteBatchPanel {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  readonly portfolioId = input.required<number>();
  readonly positions = input.required<Position[]>();
  readonly closed = output();
  readonly saved = output<number>();
  readonly prices = signal<Record<number, string>>({});
  readonly saving = signal(false);
  readonly review = signal(false);
  readonly uncertain = signal(false);
  readonly error = signal('');
  readonly confirmClose = signal(false);
  private requestId = createRequestId();
  readonly formatNumber = formatBrazilianNumber;
  readonly rows = computed(() => this.positions().filter(p => Number(p.quantity) > 0).map(position => {
    const raw = this.prices()[position.id]?.trim() ?? '';
    let total: string | null = null, problem = '';
    const price = parseBrazilianNumber(raw);
    if (raw) {
      if (new FormControl(raw, brazilianNumberValidator(15, 4)).invalid || Number(price) <= 0) problem = 'Informe uma cotação positiva, no padrão brasileiro, com até quatro casas decimais.';
      else if (!position.updatedOn) problem = 'Falta a data da posição. Recarregue a carteira.';
      else { try { total = quoteTotal(position.quantity, price); } catch { problem = 'O total calculado excede o limite da posição.'; } }
    }
    return { position, price, total, problem };
  }));
  readonly changed = computed(() => this.rows().filter(r => r.total !== null));
  readonly valid = computed(() => this.changed().length > 0 && this.changed().length <= 200 && this.rows().every(r => !r.problem));
  setPrice(id: number, value: string) { this.prices.update(values => ({ ...values, [id]: value })); this.review.set(false); this.error.set(''); this.requestId = createRequestId(); }
  cancel() { if (this.saving()) return; if (Object.values(this.prices()).some(v => v.trim())) this.confirmClose.set(true); else this.closed.emit(); }
  save() {
    if (!this.valid() || !this.review() || this.saving()) return;
    this.saving.set(true); this.error.set('');
    const items = this.changed().map(r => ({ positionId: r.position.id, currencyCode: r.position.currencyCode,
      unitPrice: r.price, expectedQuantity: r.position.quantity, expectedValue: r.position.currentValue, expectedUpdatedOn: r.position.updatedOn }));
    this.http.put(`/api/portfolios/${this.portfolioId()}/quotes`, { requestId: this.requestId, items })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => { this.saving.set(false); this.saved.emit(items.length); },
        error: error => { this.saving.set(false); this.error.set(apiError(error)); this.uncertain.set(error.status === 0 || error.status >= 500); },
      });
  }
}
