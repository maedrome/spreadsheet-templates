import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TemplateDetail } from './catalogo.models';
import { NotFound } from './not-found';
import { PageMeta } from './page-meta';
import { BuyButton } from './ui/buy-button';
import { PreviewImage } from './ui/preview-image';
import { PricePipe } from './ui/price.pipe';
import { SheetFrame } from './ui/sheet-frame';

@Component({
  selector: 'app-template-detail',
  imports: [RouterLink, PreviewImage, PricePipe, BuyButton, NotFound, SheetFrame],
  template: `
    @let template = data();
    @if (notFound()) {
      <app-not-found />
    } @else if (resource.error()) {
      <p role="alert" class="mx-auto max-w-6xl px-4 py-14 text-ink-muted">
        No pudimos cargar la plantilla.
      </p>
    } @else if (template) {
      <article
        class="mx-auto grid max-w-6xl items-start gap-8 px-4 py-8 lg:grid-cols-[3fr_2fr] lg:gap-12 lg:py-12"
      >
        <section aria-label="Vistas previas">
          @if (selectedPreview(); as preview) {
            <app-sheet-frame
              class="rounded-sheet border border-line"
              [file]="template.slug + '.xlsx'"
            >
              <app-preview-image
                [preview]="preview"
                [alt]="'Vista previa ' + (selected() + 1) + ' de ' + template.name"
                sizes="(min-width: 1024px) 60vw, 100vw"
                [priority]="true"
              />
            </app-sheet-frame>
          }
          @if (template.previews.length > 1) {
            <ul class="mt-3 grid grid-cols-4 gap-2">
              @for (preview of template.previews; track $index) {
                <li>
                  <button
                    type="button"
                    class="block w-full overflow-hidden rounded-cell border bg-surface transition"
                    [class.border-accent]="$index === selected()"
                    [class.ring-2]="$index === selected()"
                    [class.ring-accent]="$index === selected()"
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

        <section class="lg:sticky lg:top-6">
          <nav aria-label="Ruta" class="mb-4 font-mono text-xs text-ink-subtle">
            <a routerLink="/" class="hover:text-accent hover:underline">Inicio</a> /
            <span>{{ template.name }}</span>
          </nav>
          <h1 class="text-4xl font-extrabold tracking-[-0.03em] text-ink">{{ template.name }}</h1>
          <p class="mt-3 font-mono text-3xl font-bold text-ink">
            {{ template.priceCents | price }}
          </p>
          <p class="mt-5 whitespace-pre-line text-ink-muted">{{ template.description }}</p>
          <ul
            class="mt-6 divide-y divide-line rounded-cell border border-line bg-surface text-sm text-ink"
            aria-label="Incluye"
          >
            <li class="flex items-center justify-between gap-3 px-4 py-3">
              Archivo de Excel <span class="font-mono text-xs text-ink-subtle">.xlsx</span>
            </li>
            <li class="flex items-center justify-between gap-3 px-4 py-3">
              Versión de Google Sheets
              <span class="font-mono text-xs text-ink-subtle">copia</span>
            </li>
          </ul>
          <div class="mt-6"><app-buy-button tipo="plantilla" [slug]="template.slug" /></div>
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
