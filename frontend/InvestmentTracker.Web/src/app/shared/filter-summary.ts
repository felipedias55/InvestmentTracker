import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-filter-summary', standalone: true, imports: [MatButtonModule],
  template: `
    <div class="filter-summary" role="status">
      <p><strong>Filtros aplicados:</strong> {{ description() || 'Nenhum' }}.</p>
      @if (pending) { <p>Há filtros em edição. Use Aplicar filtros para atualizar os resultados.</p> }
      @if (description()) { <button mat-stroked-button type="button" (click)="cleared.emit()">Limpar filtros aplicados</button> }
    </div>
  `,
  styles: [`.filter-summary { margin:12px 0; padding:12px; border:1px solid var(--border); border-radius:8px; overflow-wrap:anywhere; } p { margin:0 0 8px; }`],
})
export class FilterSummary {
  @Input() filters: Record<string, string> = {};
  @Input() pending = false;
  @Output() cleared = new EventEmitter<void>();
  description() { return Object.entries(this.filters).filter(([,value]) => value).map(([name,value]) => {
    const displayed = /^\d{4}-\d{2}-\d{2}$/.test(value) ? value.split('-').reverse().join('/') : value;
    return `${name}: ${displayed}`;
  }).join(' · '); }
}
