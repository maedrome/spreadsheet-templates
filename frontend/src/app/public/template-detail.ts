import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TemplateDetail } from './catalogo.models';
import { NotFound } from './not-found';
import { PageMeta } from './page-meta';
import { BuyButton } from './ui/buy-button';
import { PreviewImage } from './ui/preview-image';
import { PricePipe } from './ui/price.pipe';

@Component({
  selector: 'app-template-detail',
  imports: [RouterLink, PreviewImage, PricePipe, BuyButton, NotFound],
  template: `
    @let template = data();
    @if (notFound()) {
      <app-not-found />
    } @else if (resource.error()) {
      <p role="alert" class="mx-auto max-w-6xl px-4 py-14 text-ink-muted">
        No pudimos cargar la plantilla.
      </p>
    } @else if (template) {
      <article class="mx-auto grid max-w-6xl gap-8 px-4 py-8 lg:grid-cols-[3fr_2fr] lg:py-12">
        <section aria-label="Vistas previas">
          @if (selectedPreview(); as preview) {
            <app-preview-image
              class="overflow-hidden rounded-xl border border-line"
              [preview]="preview"
              [alt]="'Vista previa ' + (selected() + 1) + ' de ' + template.name"
              sizes="(min-width: 1024px) 60vw, 100vw"
              [priority]="true"
            />
          }
          @if (template.previews.length > 1) {
            <ul class="mt-3 grid grid-cols-4 gap-2">
              @for (preview of template.previews; track $index) {
                <li>
                  <button
                    type="button"
                    class="block w-full overflow-hidden rounded border-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                    [class.border-accent]="$index === selected()"
                    [class.border-line]="$index !== selected()"
                    [attr.aria-label]="'Ver vista previa ' + ($index + 1)"
                    [attr.aria-pressed]="$index === selected()"
                    (click)="selected.set($index)"
                  >
                    <app-preview-image [preview]="preview" alt="" sizes="120px" />
                  </button>
                </li>
              }
            </ul>
          }
        </section>

        <section>
          <nav aria-label="Ruta" class="mb-3 text-sm text-ink-muted">
            <a routerLink="/" class="hover:text-accent">Inicio</a> /
            <span>{{ template.name }}</span>
          </nav>
          <h1 class="text-3xl font-bold tracking-tight text-ink">{{ template.name }}</h1>
          <p class="mt-2 text-3xl font-bold text-ink">{{ template.priceCents | price }}</p>
          <p class="mt-4 whitespace-pre-line text-ink-muted">{{ template.description }}</p>
          <ul class="mt-4 list-disc space-y-1 pl-5 text-ink-muted">
            <li>Archivo de Excel (.xlsx)</li>
            <li>Versión de Google Sheets</li>
          </ul>
          <div class="mt-6"><app-buy-button /></div>
        </section>
      </article>
    } @else {
      <p class="mx-auto max-w-6xl px-4 py-14 text-ink-muted" aria-live="polite">Cargando…</p>
    }
  `,
})
export class TemplateDetailPage {
  readonly slug = input.required<string>();

  protected readonly resource = httpResource<TemplateDetail>(
    () => `/api/plantillas/${encodeURIComponent(this.slug())}`,
  );
  protected readonly data = computed(() =>
    this.resource.hasValue() ? this.resource.value() : undefined,
  );
  protected readonly selected = signal(0);
  protected readonly selectedPreview = computed(
    () => this.data()?.previews[this.selected()] ?? null,
  );
  protected readonly notFound = computed(() => {
    const error = this.resource.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  constructor() {
    const pageMeta = inject(PageMeta);
    effect(() => {
      const template = this.data();
      if (template) {
        this.selected.set(0);
        pageMeta.set({ title: template.name, description: summarize(template.description) });
      }
    });
  }
}

function summarize(text: string): string {
  return text.length <= 155 ? text : text.slice(0, 154).trimEnd() + '…';
}
