import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BundleDetail } from './catalogo.models';
import { NotFound } from './not-found';
import { PageMeta } from './page-meta';
import { BuyButton } from './ui/buy-button';
import { PreviewImage } from './ui/preview-image';
import { PricePipe } from './ui/price.pipe';

@Component({
  selector: 'app-bundle-detail',
  imports: [RouterLink, PreviewImage, PricePipe, BuyButton, NotFound],
  template: `
    @let bundle = data();
    @if (notFound()) {
      <app-not-found />
    } @else if (resource.error()) {
      <p role="alert" class="mx-auto max-w-6xl px-4 py-14 text-ink-muted">
        No pudimos cargar el paquete.
      </p>
    } @else if (bundle) {
      <article class="mx-auto max-w-6xl px-4 py-8 lg:py-12">
        <nav aria-label="Ruta" class="mb-3 text-sm text-ink-muted">
          <a routerLink="/" class="hover:text-accent">Inicio</a> / <span>{{ bundle.name }}</span>
        </nav>
        <h1 class="text-3xl font-bold tracking-tight text-ink">{{ bundle.name }}</h1>
        <p class="mt-4 max-w-2xl whitespace-pre-line text-ink-muted">{{ bundle.description }}</p>
        <div class="mt-4 flex flex-wrap items-center gap-3">
          <p class="text-3xl font-bold text-ink">{{ bundle.priceCents | price }}</p>
          <p class="rounded-full bg-accent-soft px-3 py-1 text-sm font-semibold text-accent-strong">
            Ahorras {{ bundle.savingsCents | price }}
          </p>
        </div>
        <div class="mt-4"><app-buy-button tipo="paquete" [slug]="bundle.slug" /></div>

        <h2 class="mt-10 text-2xl font-bold text-ink">
          Incluye {{ bundle.templates.length }} plantillas
        </h2>
        <ul class="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          @for (template of bundle.templates; track template.slug; let first = $first) {
            <li>
              <a
                [routerLink]="['/plantillas', template.slug]"
                class="group block overflow-hidden rounded-xl border border-line bg-surface hover:border-accent"
              >
                @if (template.mainPreview; as preview) {
                  <app-preview-image
                    class="border-b border-line bg-sunken"
                    [preview]="preview"
                    [alt]="'Vista previa de ' + template.name"
                    sizes="(min-width: 1024px) 384px, (min-width: 640px) 50vw, 100vw"
                    [priority]="first"
                  />
                }
                <div class="p-4">
                  <h3 class="font-semibold text-ink group-hover:text-accent">
                    {{ template.name }}
                  </h3>
                  <p class="text-sm text-ink-muted">
                    Precio individual: {{ template.priceCents | price }}
                  </p>
                </div>
              </a>
            </li>
          }
        </ul>
      </article>
    } @else {
      <p class="mx-auto max-w-6xl px-4 py-14 text-ink-muted" aria-live="polite">Cargando…</p>
    }
  `,
})
export class BundleDetailPage {
  readonly slug = input.required<string>();

  protected readonly resource = httpResource<BundleDetail>(
    () => `/api/paquetes/${encodeURIComponent(this.slug())}`,
  );
  protected readonly data = computed(() =>
    this.resource.hasValue() ? this.resource.value() : undefined,
  );
  protected readonly notFound = computed(() => {
    const error = this.resource.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  constructor() {
    const pageMeta = inject(PageMeta);
    effect(() => {
      const bundle = this.data();
      if (bundle) {
        const description = bundle.description;
        pageMeta.set({
          title: bundle.name,
          description:
            description.length <= 155 ? description : description.slice(0, 154).trimEnd() + '…',
        });
      }
    });
  }
}
