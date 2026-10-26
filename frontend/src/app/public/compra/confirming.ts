import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { PageMeta } from '../page-meta';
import { Checkout } from './checkout';

/** Cada cuánto se consulta el estado y cuántas veces: unos 60 s en total. */
export const POLL_INTERVAL_MS = 2000;
export const MAX_POLLS = 30;

type State = 'confirming' | 'check-email' | 'slow' | 'invalid';

/**
 * A esta página vuelve Polar tras el pago (`success_url`). Espera a que llegue el webhook y lleva a la Compra;
 * si el enlace ya se entregó o el pago tarda, pide al cliente que revise su email.
 */
@Component({
  selector: 'app-confirming',
  template: `
    <section class="mx-auto max-w-xl px-4 py-20 text-center" aria-live="polite">
      @switch (state()) {
        @case ('confirming') {
          <div
            class="mx-auto mb-6 size-10 animate-spin rounded-full border-4 border-line border-t-accent motion-reduce:animate-none"
            aria-hidden="true"
          ></div>
          <h1 class="text-2xl font-bold text-ink">Confirmando tu pago...</h1>
          <p class="mt-3 text-ink-muted">
            Esto suele tardar unos segundos. No cierres esta página.
          </p>
        }
        @case ('check-email') {
          <h1 class="text-2xl font-bold text-ink">¡Tu pago está confirmado!</h1>
          <p class="mt-3 text-ink-muted">
            Te enviamos por email el enlace para descargar tus plantillas. Revisa tu bandeja de
            entrada y la carpeta de spam.
          </p>
        }
        @case ('slow') {
          <h1 class="text-2xl font-bold text-ink">Tu pago está tardando en confirmarse</h1>
          <p class="mt-3 text-ink-muted">
            No te preocupes: apenas se confirme, te enviaremos por email el enlace para descargar
            tus plantillas. Revisa tu bandeja de entrada y la carpeta de spam en unos minutos.
          </p>
        }
        @case ('invalid') {
          <h1 class="text-2xl font-bold text-ink">No encontramos tu pago</h1>
          <p class="mt-3 text-ink-muted">
            El enlace de confirmación no es válido. Si pagaste, te llegará un email con el enlace
            para descargar tus plantillas.
          </p>
        }
      }
    </section>
  `,
})
export class ConfirmingPage implements OnInit {
  /** El id de la sesión de checkout, en `?checkout=` desde la `success_url`. */
  readonly checkout = input<string>();

  private readonly api = inject(Checkout);
  private readonly router = inject(Router);
  protected readonly state = signal<State>('confirming');
  private timer: ReturnType<typeof setTimeout> | undefined;
  private destroyed = false;

  constructor() {
    inject(PageMeta).set({
      title: 'Confirmando tu pago',
      description: 'Estamos confirmando tu pago.',
      noindex: true,
    });
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      clearTimeout(this.timer);
    });
  }

  ngOnInit(): void {
    const checkoutId = this.checkout();
    if (!checkoutId) {
      this.state.set('invalid');
      return;
    }
    void this.poll(checkoutId, 1);
  }

  private async poll(checkoutId: string, attempt: number): Promise<void> {
    try {
      const status = await this.api.status(checkoutId);
      if (this.destroyed) return;
      if (status.status === 'paid') {
        if (status.token) {
          await this.router.navigate(['/compra', status.token], { replaceUrl: true });
        } else {
          this.state.set('check-email');
        }
        return;
      }
    } catch (error) {
      if (this.destroyed) return;
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.state.set('invalid');
        return;
      }
      // Un error de red o un 429 no termina la espera: se reintenta en la próxima vuelta.
    }

    if (attempt >= MAX_POLLS) {
      this.state.set('slow');
      return;
    }
    this.timer = setTimeout(() => void this.poll(checkoutId, attempt + 1), POLL_INTERVAL_MS);
  }
}
