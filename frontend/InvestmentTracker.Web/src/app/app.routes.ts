import { Routes } from '@angular/router';
import { catalogs } from './features/catalogs/catalog.models';
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'assets' },
  {
    path: 'assets',
    loadComponent: () => import('./features/assets/assets-page').then((m) => m.AssetsPage),
  },
  ...catalogs.map((catalog) => ({
    path: catalog.key,
    data: { catalog: catalog.key },
    loadComponent: () => import('./features/catalogs/catalog-page').then((m) => m.CatalogPage),
  })),
  { path: '**', redirectTo: 'assets' },
];
