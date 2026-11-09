import { Component, computed, effect, input, signal } from '@angular/core';

@Component({ selector: 'app-asset-identity', standalone: true,
  template: `<span class="identity" [class.large]="large()">
    <span class="picture" aria-hidden="true">@if (source() && !failed()) { <img [src]="source()!" alt="" [width]="large() ? 40 : 32" [height]="large() ? 40 : 32" loading="lazy" decoding="async" (error)="failed.set(true)" /> }
      @else { <span class="fallback">{{ ticker().slice(0, 2).toUpperCase() || '—' }}</span> }</span>
    @if (tickerOnly()) { <strong class="asset-name">{{ ticker() }}</strong> }
    @if (!tickerOnly()) { <span class="copy"><strong class="asset-name">{{ name() }}</strong><small class="ticker">{{ ticker() }}</small></span> }
  </span>`,
  styles: [':host { display:block; min-width:0; } .identity { display:flex; gap:10px; align-items:center; min-width:0; } .picture { display:grid; place-items:center; width:32px; height:32px; flex:0 0 32px; border:1px solid var(--border); border-radius:8px; overflow:hidden; background:#fff; } img { display:block; width:100%; height:100%; object-fit:contain; box-sizing:border-box; } .fallback { color:#34465a; font-size:12px; font-weight:700; } .large .picture { width:40px; height:40px; flex-basis:40px; } .copy { min-width:0; overflow-wrap:anywhere; } .asset-name,.ticker { display:block; } .asset-name { line-height:1.35; } .ticker { opacity:.75; font-size:12px; line-height:1.5; }']
})
export class AssetIdentity {
  readonly assetId = input<number | null>(null); readonly version = input<string | null | undefined>(null);
  readonly ticker = input(''); readonly name = input(''); readonly large = input(false); readonly preview = input<string | null>(null);
  readonly tickerOnly = input(false);
  readonly failed = signal(false);
  readonly source = computed(() => this.preview()?.startsWith('data:image/png;base64,') ? this.preview()
    : this.assetId() && /^[a-f0-9]{64}$/.test(this.version() ?? '') ? `/api/assets/${this.assetId()}/image?v=${this.version()}` : null);
  constructor() { effect(() => { this.source(); this.failed.set(false); }); }
}
