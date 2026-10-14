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

interface QuoteInfo { provider: string; date: string | null; price: string | null; message: string; marketTime?: string | null; retrievedAt?: string | null; }


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
    <app-edit-panel [editing]="true" [busy]="saving() || fetching()" [error]="error()" title="Atualizar cotações em lote" (closed)="cancel()">
      <section class="panel">
        <h2>{{ review() ? 'Conferir cotações' : 'Atualizar cotações' }}</h2>
        <p>Informe a cotação <strong>unitária na moeda original</strong>. Apenas os ativos filtrados com quantidade positiva estão nesta lista. Deixe em branco os que não deseja atualizar.</p>
        <p class="hint">Quantidade, custo investido e proventos serão preservados. A atualização será registrada com a data de hoje e poderá deixar a fotografia de hoje desatualizada.</p>
        @if (!review()) {
          <button (click)="fetchQuotes()" [disabled]="saving() || fetching() || uncertain() || !rows().length || rows().length > 200">{{ fetching() ? 'Consultando provedores…' : 'Consultar cotações gratuitas' }}</button>
          <p class="hint">Consulta apenas campos vazios. Brasil: brapi com atraso. EUA: Alpha Vantage, fechamento diário. Chaves gratuitas devem estar configuradas na API. A consulta não salva posições.</p>
        }
        <fieldset [disabled]="saving() || fetching() || uncertain()">
          @for (row of review() ? changed() : rows(); track row.position.id) {
            <div class="quote-row">
              <strong>{{ row.position.ticker }} · {{ row.position.currencyCode }}</strong>
              <small>{{ row.position.name }} · Quantidade: {{ formatNumber(row.position.quantity) }}</small>
              @if (!review()) {
                <label [for]="'quote-' + row.position.id">Nova cotação de {{ row.position.ticker }}</label>
                <input [id]="'quote-' + row.position.id" inputmode="decimal" autocomplete="off" placeholder="Ex.: 123,45" [value]="prices()[row.position.id] || ''" (input)="setPrice(row.position.id, $any($event.target).value)" />
              }
              @if (quotes()[row.position.id]; as quote) {

                @if (quote.price !== null) {
                  <span class="quote-info">
                    <button type="button" class="quote-info-button" aria-label="Informações da cotação consultada"
                      [attr.aria-expanded]="infoId() === row.position.id" [attr.aria-describedby]="'quote-info-' + row.position.id"
                      (click)="infoId.set(infoId() === row.position.id ? null : row.position.id)" (keydown.escape)="infoId.set(null)">ⓘ</button>
                    <span role="tooltip" [id]="'quote-info-' + row.position.id" [class.open]="infoId() === row.position.id">{{ quoteDetails(quote) }}</span>
                  </span>
                } @else { <p class="hint">{{ quote.message }}</p> }
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
            <button class="primary" (click)="save()" [disabled]="saving() || fetching()">{{ saving() ? 'Salvando lote…' : uncertain() ? 'Tentar novamente' : 'Confirmar atualização do lote' }}</button>
            <button (click)="review.set(false)" [disabled]="saving() || fetching() || uncertain()">Voltar à edição</button>
          } @else { <button class="primary" (click)="review.set(true)" [disabled]="!valid() || fetching()">Revisar {{ changed().length }} cotações</button> }
          <button (click)="cancel()" [disabled]="saving() || fetching()">Cancelar</button>
          @if (confirmClose()) { <p>Fechar e descartar os campos preenchidos? Se houve falha de comunicação, confira a carteira antes de iniciar outro lote.</p><button (click)="closed.emit()">Fechar sem continuar</button><button (click)="confirmClose.set(false)">Continuar edição</button> }
        </div>
      </section>
    </app-edit-panel>
  `,
  styles: [`
    .quote-info { position: relative; display: inline-block; margin-top: 6px; }
    .quote-info-button { padding: 3px 8px; color: var(--muted); background: transparent; }
    [role=tooltip] { display: none; position: absolute; left: 0; bottom: 100%; z-index: 2; width: min(300px, 65vw); padding: 12px; white-space: pre-line; background: #252525; color: white; border-radius: 6px; font-size: 12px; line-height: 1.6; box-shadow: 0 3px 12px #0003; }
    .quote-info:hover [role=tooltip], .quote-info:focus-within [role=tooltip], [role=tooltip].open { display: block; }
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
  readonly fetching = signal(false);
  readonly quotes = signal<Record<number, QuoteInfo>>({});
  fetchQuotes() {
    if (this.fetching() || this.saving() || this.uncertain() || this.review()) return;
    const ids = this.rows().filter(r => !(this.prices()[r.position.id] || '').trim()).map(r => r.position.id);
    if (!ids.length || ids.length > 200) { this.error.set('Deixe em branco os campos que deseja consultar, até 200 posições.'); return; }
    this.fetching.set(true); this.error.set('');
    this.http.post<{ positionId: number; quote: QuoteInfo }[]>(
      '/api/portfolios/' + this.portfolioId() + '/quotes/preview', { positionIds: ids })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: results => {
          const prices = { ...this.prices() }, quotes = { ...this.quotes() };
          for (const item of results) {
            quotes[item.positionId] = item.quote;
            if (item.quote.price !== null) prices[item.positionId] = formatBrazilianNumber(item.quote.price);
          }
          this.prices.set(prices); this.quotes.set(quotes); this.requestId = createRequestId(); this.fetching.set(false);
        },
        error: error => { this.fetching.set(false); this.error.set(apiError(error)); }
      });
  }

  readonly infoId = signal<number | null>(null);
  quoteDetails(quote: QuoteInfo): string {
    const instant = (value: string) => new Intl.DateTimeFormat('pt-BR', {
      timeZone: 'America/Sao_Paulo', dateStyle: 'short', timeStyle: 'medium'
    }).format(new Date(value)) + ' (Brasília)';
    const date = quote.date ? quote.date.split('-').reverse().join('/') : 'não informada';
    return [
      'Cotação consultada · ' + quote.provider,
      quote.marketTime ? 'Horário de mercado: ' + instant(quote.marketTime) : 'Fechamento: ' + date + ' · horário não fornecido pelo provedor',
      quote.retrievedAt ? 'Consulta ao provedor: ' + instant(quote.retrievedAt) : 'Horário da consulta não disponível',
      quote.message,
      'O valor só é salvo na carteira após confirmar o lote.'
    ].join('\n');
  }
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
  setPrice(id: number, value: string) { this.quotes.update(quotes => { const next = { ...quotes }; delete next[id]; return next; }); this.prices.update(values => ({ ...values, [id]: value })); this.review.set(false); this.error.set(''); this.requestId = createRequestId(); }
  cancel() { if (this.saving() || this.fetching()) return; if (Object.values(this.prices()).some(v => v.trim())) this.confirmClose.set(true); else this.closed.emit(); }
  save() {
    if (!this.valid() || !this.review() || this.saving() || this.fetching()) return;
    this.saving.set(true); this.error.set('');
    const items = this.changed().map(r => ({ positionId: r.position.id, currencyCode: r.position.currencyCode,
      unitPrice: r.price, expectedQuantity: r.position.quantity, expectedValue: r.position.currentValue, expectedUpdatedOn: r.position.updatedOn,
      source: this.quotes()[r.position.id]?.price ? this.quotes()[r.position.id].provider : undefined,
      quoteDate: this.quotes()[r.position.id]?.price ? this.quotes()[r.position.id].date : undefined }));
    this.http.put(`/api/portfolios/${this.portfolioId()}/quotes`, { requestId: this.requestId, items })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => { this.saving.set(false); this.saved.emit(items.length); },
        error: error => { this.saving.set(false); this.error.set(apiError(error)); this.uncertain.set(error.status === 0 || error.status >= 500); },
      });
  }
}
