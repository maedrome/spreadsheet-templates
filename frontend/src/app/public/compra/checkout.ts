import { HttpClient } from '@angular/common/http';
import { DOCUMENT, InjectionToken, Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CheckoutCreated, CheckoutStatus, ProductType } from './compra.models';

/** Sale de la SPA hacia otra página. Se reemplaza en los tests. */
export const EXTERNAL_REDIRECT = new InjectionToken<(url: string) => void>('EXTERNAL_REDIRECT', {
  providedIn: 'root',
  factory: () => {
    const document = inject(DOCUMENT);
    return (url) => document.location.assign(url);
  },
});

@Service()
export class Checkout {
  private readonly http = inject(HttpClient);
  private readonly redirect = inject(EXTERNAL_REDIRECT);

  /** Crea la sesión de pago y lleva al cliente al checkout alojado de Polar. */
  async start(tipo: ProductType, slug: string): Promise<void> {
    const { url } = await firstValueFrom(
      this.http.post<CheckoutCreated>('/api/checkout', { tipo, slug }),
    );
    this.redirect(url);
  }

  status(checkoutId: string): Promise<CheckoutStatus> {
    return firstValueFrom(
      this.http.get<CheckoutStatus>(`/api/checkout/${encodeURIComponent(checkoutId)}/estado`),
    );
  }
}
