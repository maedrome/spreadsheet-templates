import { InjectionToken } from '@angular/core';

/** Nombre de la tienda. Placeholder hasta que se defina la marca. */
export const STORE_NAME = new InjectionToken<string>('STORE_NAME', {
  providedIn: 'root',
  factory: () => 'Plantillas de Presupuesto',
});
