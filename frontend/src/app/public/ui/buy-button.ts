import { Component } from '@angular/core';

/** El checkout llega en la Fase 4: por ahora el botón queda deshabilitado. */
@Component({
  selector: 'app-buy-button',
  template: `
    <button
      type="button"
      disabled
      class="w-full cursor-not-allowed rounded-lg bg-ink-muted px-5 py-3 text-lg font-semibold text-white sm:w-auto"
    >
      Comprar
    </button>
    <p class="mt-2 text-sm text-ink-muted">La compra estará disponible muy pronto.</p>
  `,
})
export class BuyButton {}
