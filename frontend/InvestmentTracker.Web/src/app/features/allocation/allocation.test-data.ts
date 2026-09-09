import { Dashboard } from './allocation.service';
export const testPortfolio = {
  id: 1,
  name: 'Principal',
  description: null,
  baseCurrencyId: 1,
  baseCurrencyCode: 'BRL',
};
export const testDashboard: Dashboard = {
  externalAssets: {
    portfolio: testPortfolio,
    items: [],
    totalValue: '0',
    conversionAvailable: true,
    hasStaleRates: false,
    hasFallbackRates: false,
  },
  totalWealth: '1234.50',
  summary: {
    portfolio: testPortfolio,
    positions: [],
    originalSubtotals: [],
    totalInvested: '1000',
    totalIncome: '0',
    currentValue: '1234.50',
    conversionAvailable: true,
    hasStaleRates: false,
    hasFallbackRates: false,
  },
  allocation: {
    categoryTargetsConfigured: true,
    sectorTargetsConfigured: false,
    categories: [
      {
        groupId: 1,
        name: 'Ações',
        currentValue: '1234.50',
        currentPercentage: '1',
        targetPercentage: '0.75',
        difference: '-0.25',
      },
    ],
    sectors: [],
    countries: [],
  },
};
