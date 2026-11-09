import { matchSector } from './sector-matching';

describe('sector matching', () => {
  it('recognizes translated broad sectors and normalized local names', () => {
    expect(matchSector('Consumer Cyclical', [{ id: 4, name: 'Consumo Cíclico' }])).toMatchObject({ kind: 'alias', candidates: [{ id: 4 }] });
    expect(matchSector('  SAUDE  ', [{ id: 2, name: 'Saúde' }]).kind).toBe('exact');
    expect(matchSector('Financial Services', [{ id: 3, name: 'Financeiro' }]).kind).toBe('alias');
  });
  it('requires a choice when exact and translated names coexist instead of prioritizing one ID', () => {
    expect(matchSector('Technology', [{ id: 1, name: 'Technology' }, { id: 2, name: 'Tecnologia' }])).toMatchObject({ kind: 'ambiguous', candidates: [{ id: 1 }, { id: 2 }] });
  });
  it('does not infer broad sectors from subindustries, fund types or similar text', () => {
    const sectors = [{ id: 1, name: 'Financeiro' }, { id: 2, name: 'Imobiliário' }, { id: 3, name: 'Energia' }];
    for (const source of ['Banks', 'REIT', 'Technology Services', 'Utilities']) expect(matchSector(source, sectors).kind).toBe('missing');
    expect(matchSector(null, sectors).kind).toBe('unavailable');
  });
});
