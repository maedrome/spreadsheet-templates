import { InjectionToken } from '@angular/core';
import { LEGAL_INFO } from './legal/legal-info';

/** Nombre de la tienda para el layout y los títulos. El valor vive en `LEGAL_INFO.storeName`; el token permite reemplazarlo en tests. */
export const STORE_NAME = new InjectionToken<string>('STORE_NAME', {
  providedIn: 'root',
  factory: () => LEGAL_INFO.storeName,
});
