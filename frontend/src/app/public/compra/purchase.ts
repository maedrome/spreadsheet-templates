import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageMeta } from '../page-meta';
import { PricePipe } from '../ui/price.pipe';
import { Purchase } from './compra.models';

const paidDate = new Intl.DateTimeFormat('es-EC', { dateStyle: 'long' });

/** La página del Enlace de compra: el resumen de la Compra. Las descargas llegan en la fase 5. */
@Component({
  selector: 'app-purchase',
  imports: [RouterLink, PricePipe],
  template: `
    <section class="mx-auto max-w-2xl px-4 py-12">
      @if (notFound()) {
        <h1 class="text-2xl font-bold text-ink">Este enlace no es válido</h1>
        <p class="mt-3 text-ink-muted">
          Revisa que hayas copiado el enlace completo del email de tu compra.
        </p>
      } @else if (resource.error()) {
        <p role="alert" class="text-ink-muted">No pudimos cargar tu compra. Recarga la página.</p>
      } @else if (data(); as purchase) {
        <h1 class="text-3xl font-bold tracking-tight text-ink">Tu compra</h1>
        @if (purchase.isRefunded) {
          <p role="status" class="mt-6 rounded-lg border border-line bg-sunken p-4 text-ink">
            Esta compra fue reembolsada, así que su enlace ya no da acceso a las descargas.
          </p>
        }
        <dl
          class="mt-6 grid gap-x-6 gap-y-3 rounded-xl border border-line bg-surface p-6 sm:grid-cols-[auto_1fr]"
        >
          <dt class="font-semibold text-ink">
            {{ purchase.productType === 'plantilla' ? 'Plantilla' : 'Paquete' }}
          </dt>
          <dd class="text-ink-muted">{{ purchase.productName }}</dd>
          <dt class="font-semibold text-ink">Monto</dt>
          <dd class="text-ink-muted">{{ purchase.pricePaidCents | price }}</dd>
          <dt class="font-semibold text-ink">Fecha</dt>
          <dd class="text-ink-muted">{{ paidOn() }}</dd>
          <dt class="font-semibold text-ink">Email</dt>
          <dd class="break-all text-ink-muted">{{ purchase.email }}</dd>
        </dl>

        <h2 class="mt-10 text-xl font-bold text-ink">
          {{ purchase.templates.length === 1 ? 'Plantilla incluida' : 'Plantillas incluidas' }}
        </h2>
        <ul class="mt-4 divide-y divide-line rounded-xl border border-line bg-surface">
          @for (template of purchase.templates; track template.id) {
            <li
              class="flex flex-col gap-3 px-6 py-4 sm:flex-row sm:items-center sm:justify-between"
            >
              <a
                [routerLink]="['/plantillas', template.slug]"
                class="font-medium text-ink hover:text-accent"
              >
                {{ template.name }}
              </a>
              @if (template.downloadUrl) {
                <div class="flex flex-col gap-2 sm:flex-row sm:items-center">
                  <a
                    [href]="template.downloadUrl"
                    download
                    class="rounded-lg bg-accent px-4 py-2 text-center font-semibold text-white hover:bg-accent-strong focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                  >
                    Descargar .xlsx
                  </a>
                  @if (template.googleSheetsCopyUrl) {
                    <a
                      [href]="template.googleSheetsCopyUrl"
                      target="_blank"
                      rel="noopener noreferrer"
                      class="rounded-lg border border-line px-4 py-2 text-center font-semibold text-ink hover:text-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                    >
                      Abrir en Google Sheets
                    </a>
                  }
                </div>
              }
            </li>
          }
        </ul>
        @if (!purchase.isRefunded) {
          <p class="mt-6 text-sm text-ink-muted">
            Guarda esta página o el email de tu compra: el enlace no vence y es tu acceso a las
            descargas.
          </p>
        }
      } @else {
        <p class="text-ink-muted" aria-live="polite">Cargando…</p>
      }
    </section>
  `,
})
export class PurchasePage {
  readonly token = input.required<string>();

  protected readonly resource = httpResource<Purchase>(
    () => `/api/compras/${encodeURIComponent(this.token())}`,
  );
  protected readonly data = computed(() =>
    this.resource.hasValue() ? this.resource.value() : undefined,
  );
  protected readonly paidOn = computed(() => {
    const paidAt = this.data()?.paidAt;
    return paidAt ? paidDate.format(new Date(paidAt)) : '';
  });
  protected readonly notFound = computed(() => {
    const error = this.resource.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  constructor() {
    inject(PageMeta).set({
      title: 'Tu compra',
      description: 'El resumen de tu compra.',
      noindex: true,
    });
  }
}
