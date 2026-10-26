import { Routes } from '@angular/router';
import { sessionGuard } from './admin-guard';
import { AdminLayout } from './layout/admin-layout';

export const adminRoutes: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      {
        path: 'login',
        title: 'Iniciar sesión · Admin',
        canActivate: [sessionGuard('anonymous')],
        loadComponent: () => import('./auth/login').then((m) => m.Login),
      },
      {
        path: 'verificar',
        title: 'Verificación · Admin',
        canActivate: [sessionGuard('requiresTwoFactor')],
        loadComponent: () => import('./auth/two-factor-verify').then((m) => m.TwoFactorVerify),
      },
      {
        path: 'configurar-2fa',
        title: 'Configurar 2FA · Admin',
        canActivate: [sessionGuard('requiresEnrollment')],
        loadComponent: () => import('./auth/two-factor-setup').then((m) => m.TwoFactorSetup),
      },
      {
        path: '',
        canActivateChild: [sessionGuard('authenticated')],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'plantillas' },
          {
            path: 'plantillas',
            title: 'Plantillas · Admin',
            loadComponent: () => import('./templates/template-list').then((m) => m.TemplateList),
          },
          {
            path: 'plantillas/nueva',
            title: 'Nueva Plantilla · Admin',
            loadComponent: () => import('./templates/template-edit').then((m) => m.TemplateEdit),
          },
          {
            path: 'plantillas/:id',
            title: 'Plantilla · Admin',
            loadComponent: () => import('./templates/template-edit').then((m) => m.TemplateEdit),
          },
          {
            path: 'paquetes',
            title: 'Paquetes · Admin',
            loadComponent: () => import('./bundles/bundle-list').then((m) => m.BundleList),
          },
          {
            path: 'paquetes/nuevo',
            title: 'Nuevo Paquete · Admin',
            loadComponent: () => import('./bundles/bundle-edit').then((m) => m.BundleEdit),
          },
          {
            path: 'paquetes/:id',
            title: 'Paquete · Admin',
            loadComponent: () => import('./bundles/bundle-edit').then((m) => m.BundleEdit),
          },
          {
            path: 'compras',
            title: 'Compras · Admin',
            loadComponent: () => import('./orders/order-list').then((m) => m.OrderList),
          },
          { path: '**', redirectTo: 'plantillas' },
        ],
      },
    ],
  },
];
