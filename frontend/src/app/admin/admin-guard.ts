import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AdminSession, SESSION_HOME, SessionStatus } from './admin-session';

/**
 * Deja pasar solo si la sesión está en `required`; si no, lleva a la pantalla de su estado.
 * Consulta el servidor en cada navegación, lo que además renueva el token XSRF.
 */
export function sessionGuard(required: SessionStatus): CanActivateFn {
  return async () => {
    const session = inject(AdminSession);
    const router = inject(Router);

    let status: SessionStatus;
    try {
      status = (await session.refresh()).status;
    } catch {
      status = 'anonymous';
    }
    return status === required || router.parseUrl(SESSION_HOME[status]);
  };
}
