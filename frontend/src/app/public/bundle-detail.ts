import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BundleDetail } from './catalogo.models';
import { NotFound } from './not-found';
import { PageMeta } from './page-meta';
import { BuyButton } from './ui/buy-button';
import { PreviewImage } from './ui/preview-image';
import { PricePipe } from './ui/price.pipe';
import { SavingsTag } from './ui/savings-tag';
import { SheetFrame } from './ui/sheet-frame';

@Component({
  selector: 'app-bundle-detail',
  imports: [RouterLink, PreviewImage, PricePipe, BuyButton, NotFound, SavingsTag, SheetFrame],
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
        <div class="max-w-2xl">
          <nav aria-label="Ruta" class="mb-4 font-mono text-xs text-ink-subtle">
            <a routerLink="/" class="hover:text-accent hover:underline">Inicio</a> /
            <span>{{ bundle.name }}</span>
          </nav>
          <app-savings-tag [cents]="bundle.savingsCents" />
          <h1 class="mt-3 text-4xl font-extrabold tracking-[-0.03em] text-ink">
            {{ bundle.name }}
          </h1>
          <p class="mt-4 whitespace-pre-line text-ink-muted">{{ bundle.description }}</p>
          <p class="mt-5 font-mono text-3xl font-bold text-ink">{{ bundle.priceCents | price }}</p>
          <div class="mt-5"><app-buy-button tipo="paquete" [slug]="bundle.slug" /></div>
        </div>

        <h2 class="mt-14 text-2xl font-bold text-ink">
          Incluye {{ bundle.templates.length }} plantillas
        </h2>
        <ul class="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          @for (template of bundle.templates; track template.slug; let i = $index) {
            <li>
              <a
                [routerLink]="['/plantillas', template.slug]"
                class="group block overflow-hidden rounded-cell border border-line bg-surface transition hover:border-accent hover:shadow-lift"
              >
                @if (template.mainPreview; as preview) {
                  <app-sheet-frame class="border-b border-line" [file]="template.slug + '.xlsx'">
                    <app-preview-image
                      [preview]="preview"
                      [alt]="'Vista previa de ' + template.name"
                      sizes="(min-width: 1024px) 384px, (min-width: 640px) 50vw, 100vw"
                      [priority]="i < 3"
                    />
                  </app-sheet-frame>
                }
                <div class="flex items-center justify-between gap-3 p-4">
                  <h3 class="font-bold text-ink group-hover:text-accent-ink">
                    {{ template.name }}
                  </h3>
                  <p class="font-mono text-sm text-ink-subtle">
                    <span class="sr-only">Precio individual: </span
                    ><s>{{ template.priceCents | price }}</s>
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
