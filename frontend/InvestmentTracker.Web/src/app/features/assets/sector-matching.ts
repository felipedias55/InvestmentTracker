import { CatalogItem } from '../catalogs/catalog.models';

const normalize = (value: string) => value.trim().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/\s+/g, ' ').toLocaleLowerCase('pt-BR');

// Broad sectors only. Subindustries (e.g. banks) and fund strategies are not synonyms.
const aliases: readonly (readonly string[])[] = [
  ['technology', 'information technology', 'tecnologia', 'tecnologia da informação'],
  ['financial services', 'financials', 'serviços financeiros', 'financeiro', 'finanças'],
  ['healthcare', 'health care', 'saúde'],
  ['consumer cyclical', 'consumer discretionary', 'consumo cíclico'],
  ['consumer defensive', 'consumer staples', 'consumo não cíclico'],
  ['basic materials', 'materials', 'materiais básicos'],
  ['industrials', 'industrial', 'bens industriais'],
  ['real estate', 'imobiliário'],
  ['communication services', 'serviços de comunicação', 'comunicações'],
  ['utilities', 'utilidade pública', 'utilidades públicas'],
  ['energy', 'energia'],
];

export interface SectorMatch { kind: 'exact' | 'alias' | 'ambiguous' | 'missing' | 'unavailable'; candidates: CatalogItem[]; }

export function matchSector(source: string | null, sectors: readonly CatalogItem[]): SectorMatch {
  if (!source?.trim()) return { kind: 'unavailable', candidates: [] };
  const key = normalize(source), group = aliases.find(values => values.some(value => normalize(value) === key));
  const candidates = sectors.filter(sector => normalize(sector.name) === key || group?.some(value => normalize(value) === normalize(sector.name)));
  if (candidates.length > 1) return { kind: 'ambiguous', candidates };
  if (!candidates.length) return { kind: 'missing', candidates };
  return { kind: normalize(candidates[0].name) === key ? 'exact' : 'alias', candidates };
}
