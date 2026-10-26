import { Routes } from '@angular/router';
import { PublicLayout } from './layout';

export const publicRoutes: Routes = [
  {
    path: '',
    component: PublicLayout,
    children: [
      { path: '', loadComponent: () => import('./home').then((m) => m.Home) },
      {
        path: 'plantillas/:slug',
        loadComponent: () => import('./template-detail').then((m) => m.TemplateDetailPage),
      },
      {
        path: 'paquetes/:slug',
        loadComponent: () => import('./bundle-detail').then((m) => m.BundleDetailPage),
      },
      { path: '**', loadComponent: () => import('./not-found').then((m) => m.NotFound) },
    ],
  },
];
