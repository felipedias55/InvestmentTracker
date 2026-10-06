import { Component, computed, input } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { AllocationRow } from './allocation.service';
import { PrivateCurrencyPipe } from '../../core/value-privacy';

@Component({
  selector: 'app-allocation-donut', standalone: true, imports: [PercentPipe, PrivateCurrencyPipe],
  template: `
    @if (valid()) {
      <div class="distribution">
        <svg viewBox="0 0 200 200" role="img" aria-label="Distribuição dos grupos exibidos em relação à carteira inteira">
          <circle cx="100" cy="100" r="72" fill="none" stroke="#e8eaed" stroke-width="26" />
          @for (slice of slices(); track slice.row.groupId) {
            <circle cx="100" cy="100" r="72" fill="none" [attr.stroke]="slice.color" stroke-width="26" pathLength="100" [attr.stroke-dasharray]="slice.size + ' ' + (100 - slice.size)" [attr.stroke-dashoffset]="-slice.offset" transform="rotate(-90 100 100)"><title>{{ slice.row.name }}: {{ slice.row.currentPercentage | percent:'1.2-2' }}</title></circle>
          }
          <text x="100" y="96" text-anchor="middle" class="center">{{ share() | percent:'1.1-1' }}</text>
          <text x="100" y="116" text-anchor="middle" class="caption">da carteira</text>
        </svg>
        <ul>@for (slice of slices(); track slice.row.groupId) {
          <li><span class="swatch" [style.background]="slice.color" aria-hidden="true"></span><div><strong>{{ slice.row.name }}</strong><small>{{ slice.row.currentValue | currency:currency():'code' }}</small></div><b>{{ slice.row.currentPercentage | percent:'1.2-2' }}</b></li>
        }</ul>
      </div>
      @if (share() < 0.9999) { <p class="hint">A área cinza representa a participação não exibida pelos filtros. Os percentuais não são recalculados.</p> }
    } @else { <p class="empty">Distribuição indisponível: não há participações válidas para desenhar.</p> }
  `,
  styles: [`
    .distribution { display: grid; grid-template-columns: minmax(160px, 240px) minmax(0, 1fr); align-items: center; gap: 24px; }
    svg { width: 100%; max-width: 240px; margin: auto; }
    .center { font-size: 24px; font-weight: 700; fill: var(--ink); } .caption { font-size: 11px; fill: var(--muted); }
    ul { list-style: none; padding: 0; margin: 0; } li { display: flex; gap: 10px; align-items: center; padding: 10px 0; border-bottom: 1px solid var(--border); }
    li div { flex: 1; min-width: 0; overflow-wrap: anywhere; } small { display: block; color: var(--muted); margin-top: 5px; }
    .swatch { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
    @media(max-width: 600px) { .distribution { grid-template-columns: 1fr; gap: 8px; } }
  `],
})
export class AllocationDonut {
  readonly rows = input.required<AllocationRow[]>();
  readonly currency = input.required<string>();
  readonly share = computed(() => this.rows().reduce((sum, r) => sum + Number(r.currentPercentage ?? 0), 0));
  readonly valid = computed(() => this.rows().length > 0 && this.rows().every(r => r.currentPercentage !== null && Number(r.currentPercentage) >= 0) && this.share() > 0 && this.share() <= 1.0001);
  readonly slices = computed(() => {
    const colors = ['#ac893c', '#536c86', '#c4a75d', '#766285', '#527a78', '#929ba8', '#67728b'];
    let offset = 0;
    return this.rows().map(row => {
      const size = Math.min(100 - offset, Number(row.currentPercentage ?? 0) * 100);
      const slice = { row, size, offset, color: colors[Math.abs(row.groupId) % colors.length] };
      offset += size; return slice;
    });
  });
}
