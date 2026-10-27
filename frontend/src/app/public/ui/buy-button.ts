import { Component, inject, input, signal } from '@angular/core';
import { Checkout } from '../compra/checkout';
import { ProductType } from '../compra/compra.models';

/** Lleva al checkout de Polar. El botón queda deshabilitado mientras se crea la sesión. */
@Component({
  selector: 'app-buy-button',
  template: `
    <button
      type="button"
      class="btn btn-primary w-full text-lg sm:w-auto sm:min-w-56"
      [disabled]="pending()"
      [attr.aria-busy]="pending()"
      (click)="buy()"
    >
      {{ pending() ? 'Abriendo el pago…' : 'Comprar' }}
    </button>
    @if (failed()) {
      <p role="alert" class="mt-2 text-sm text-danger">
        No pudimos abrir el pago. Inténtalo de nuevo en unos minutos.
      </p>
    } @else {
      <p class="mt-2 text-sm text-ink-muted">
        Pago seguro con tarjeta a través de Polar. Recibes el enlace de descarga por email.
      </p>
    }
  `,
})
export class BuyButton {
  readonly tipo = input.required<ProductType>();
  readonly slug = input.required<string>();

  private readonly checkout = inject(Checkout);
  protected readonly pending = signal(false);
  protected readonly failed = signal(false);

  protected async buy(): Promise<void> {
    this.pending.set(true);
    this.failed.set(false);
    try {
      // Si todo sale bien, el navegador ya está saliendo hacia Polar: el botón sigue deshabilitado.
      await this.checkout.start(this.tipo(), this.slug());
    } catch {
      this.failed.set(true);
      this.pending.set(false);
    }
  }
}
