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
      {
        path: 'compra/confirmando',
        loadComponent: () => import('./compra/confirming').then((m) => m.ConfirmingPage),
      },
      {
        path: 'compra/:token',
        loadComponent: () => import('./compra/purchase').then((m) => m.PurchasePage),
      },
      {
        path: 'recuperar',
        loadComponent: () => import('./compra/recover').then((m) => m.RecoverPage),
      },
      {
        path: 'terminos',
        loadComponent: () => import('./legal/terms').then((m) => m.TermsPage),
      },
      {
        path: 'privacidad',
        loadComponent: () => import('./legal/privacy').then((m) => m.PrivacyPage),
      },
      { path: '**', loadComponent: () => import('./not-found').then((m) => m.NotFound) },
    ],
  },
];
