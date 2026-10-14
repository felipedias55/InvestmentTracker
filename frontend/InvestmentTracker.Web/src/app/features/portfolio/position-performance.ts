import { Component, computed, input } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { PrivateCurrencyPipe } from '../../core/value-privacy';

import type { PricePerformance } from './portfolio.service';

@Component({
  selector: 'app-position-performance', standalone: true, imports: [PercentPipe, PrivateCurrencyPipe],
  template: '<section aria-label="Variação sobre o preço médio">' +
    '<div class="performance-heading"><span>Variação sobre o preço médio</span>' +
    '@if (variation() !== null) {' +
      '<strong class="indicator" [class.gain]="variation()! > 0" [class.loss]="variation()! < 0">' +
      '<span aria-hidden="true">{{ variation()! > 0 ? "▲" : variation()! < 0 ? "▼" : "—" }}</span> ' +
      '<span class="sr-only">{{ variation()! > 0 ? "Valorização" : variation()! < 0 ? "Desvalorização" : "Sem variação" }}: </span>' +
      '{{ variation()! > 0 ? "+" : "" }}{{ performance()!.variation | percent:"1.2-4" }}</strong>' +
    '} @else { <strong class="unavailable">Sem base de cálculo</strong> }</div>' +
    '<div class="prices"><span>Preço médio · {{ currency() }}: <b>{{ performance()?.averagePrice == null ? "Indisponível" : (performance()!.averagePrice | currency:currency():"code":"1.2-4") }}</b></span>' +
    '<span>Cotação unitária · {{ currency() }}: <b>{{ performance()?.unitPrice == null ? "Indisponível" : (performance()!.unitPrice | currency:currency():"code":"1.2-4") }}</b></span></div>' +
    '<small>Moeda original · valores cadastrados · sem proventos ou variação cambial.</small></section>',
  styles: ['section { margin-top: 12px; padding: 14px; background: white; border: 1px solid var(--border); border-radius: 8px; } .performance-heading, .prices { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 20px; } .performance-heading { justify-content: space-between; } .indicator { padding: 5px 9px; border-radius: 6px; font-variant-numeric: tabular-nums; background: #edf0f3; color: #505861; } .gain { color: #17623a; background: #e8f4ec; } .loss { color: #a12e36; background: #fbeaec; } .prices { margin: 12px 0 8px; font-size: 12px; line-height: 1.6; } small, .unavailable { color: var(--muted); font-size: 12px; } b { overflow-wrap: anywhere; }']
})
export class PositionPerformance {
  readonly performance = input<PricePerformance | null>();
  readonly currency = input.required<string>();
  readonly variation = computed(() => this.performance()?.variation == null ? null : Number(this.performance()!.variation));
}
