export type CatalogKey =
  'asset-types' | 'countries' | 'currencies' | 'asset-categories' | 'sectors';
export interface CatalogItem {
  id: number;
  name: string;
  code?: string;
  symbol?: string | null;
}
export interface CatalogInput {
  name: string;
  code?: string;
  symbol?: string | null;
}
export const catalogs: { key: CatalogKey; label: string; singular: string; description: string }[] =
  [
    {
      key: 'asset-types',
      label: 'Tipos de ativo',
      singular: 'tipo de ativo',
      description: 'Organize os instrumentos que fazem parte dos seus investimentos.',
    },
    {
      key: 'countries',
      label: 'Países',
      singular: 'país',
      description: 'Classifique a presença geográfica dos seus ativos.',
    },
    {
      key: 'currencies',
      label: 'Moedas',
      singular: 'moeda',
      description: 'Defina as moedas dos ativos, independentemente do país.',
    },
    {
      key: 'asset-categories',
      label: 'Categorias',
      singular: 'categoria',
      description: 'Organize os ativos conforme sua estratégia de investimento.',
    },
    {
      key: 'sectors',
      label: 'Setores',
      singular: 'setor',
      description: 'Classifique os segmentos de atividade dos seus investimentos.',
    },
  ];
