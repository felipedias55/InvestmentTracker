import { inject } from '@angular/core';
import { UnsavedChanges } from './shared/unsaved-registry';
import { PageContexts } from './shared/page-context';
import { Routes } from '@angular/router';
import { catalogs } from './features/catalogs/catalog.models';
const pageRoutes: Routes = [
  { path: 'movements', loadComponent: () => import('./features/movements/movements-page').then(m => m.MovementsPage) },
  { path: 'income', loadComponent: () => import('./features/income/income-page').then(m => m.IncomePage) },
  { path: 'trades', loadComponent: () => import('./features/trades/trades-page').then(m => m.TradesPage) },
  {
    path: 'history',
    loadComponent: () => import('./features/history/history-page').then((m) => m.HistoryPage),
  },
  {
    path: 'external-assets',
    loadComponent: () =>
      import('./features/external-assets/external-assets-page').then((m) => m.ExternalAssetsPage),
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: 'allocation',
    loadComponent: () => import('./features/allocation/targets-page').then((m) => m.TargetsPage),
  },
  {
    path: 'contributions',
    loadComponent: () =>
      import('./features/allocation/contributions-page').then((m) => m.ContributionsPage),
  },
  {
    path: 'portfolio',
    loadComponent: () => import('./features/portfolio/portfolio-page').then((m) => m.PortfolioPage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'assets',
    loadComponent: () => import('./features/assets/assets-page').then((m) => m.AssetsPage),
  },
  ...catalogs.map((catalog) => ({
    path: catalog.key,
    data: { catalog: catalog.key },
    loadComponent: () => import('./features/catalogs/catalog-page').then((m) => m.CatalogPage),
  })),
  { path: '**', redirectTo: 'dashboard' },
];

export const routes: Routes = pageRoutes.map(route => ({ ...route, canDeactivate: [() => {
  const contexts = inject(PageContexts);
  if (!inject(UnsavedChanges).confirmWithin()) return false;
  contexts.capture();
  return true;
}] }));
