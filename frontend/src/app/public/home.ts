import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { Catalogo } from './catalogo.models';
import { Faq } from './faq';
import { PageMeta } from './page-meta';
import { BundleCardView } from './ui/bundle-card';
import { PreviewImage } from './ui/preview-image';
import { SheetFrame } from './ui/sheet-frame';
import { TemplateCardView } from './ui/template-card';

@Component({
  selector: 'app-home',
  imports: [BundleCardView, TemplateCardView, Faq, PreviewImage, SheetFrame],
  template: `
    <section class="border-b border-line bg-surface">
      <div
        aria-hidden="true"
        class="grid grid-cols-[2rem_repeat(5,1fr)] border-b border-line bg-canvas text-center font-mono text-[11px] leading-6 text-ink-subtle sm:grid-cols-[2.5rem_repeat(8,1fr)]"
      >
        <span></span>
        @for (column of columns; track column; let i = $index) {
          <span
            class="border-l border-line"
            [class.max-sm:hidden]="i >= 5"
            [class.bg-accent-soft]="i === 1"
            [class.text-accent-ink]="i === 1"
            >{{ column }}</span
          >
        }
      </div>
      <div
        class="mx-auto grid max-w-6xl items-center gap-10 px-4 py-12 sm:py-16 lg:grid-cols-[1fr_minmax(0,28rem)] lg:py-20"
      >
        <div>
          <p
            aria-hidden="true"
            class="w-fit rounded-chip bg-accent-soft px-2 py-1 font-mono text-xs text-accent-ink"
          >
            =SUMA(ingresos)-gastos
          </p>
          <h1
            class="mt-4 max-w-2xl text-4xl leading-[1.02] font-extrabold tracking-[-0.03em] text-ink sm:text-6xl"
          >
            Ordena tu dinero con plantillas
            <span class="text-accent">listas para usar</span>
          </h1>
          <p class="mt-5 max-w-xl text-lg text-ink-muted">
            Plantillas para Excel y Google Sheets. Las compras una vez, las descargas al instante y
            son tuyas para siempre.
          </p>
          <a
            href="#plantillas"
            class="btn btn-primary mt-8"
            (click)="scrollTo('plantillas', $event)"
          >
            Ver plantillas
          </a>
        </div>
        @if (featured(); as featured) {
          <app-sheet-frame
            class="hidden rounded-sheet border border-line shadow-lift lg:block"
            [file]="featured.slug + '.xlsx'"
          >
            <app-preview-image
              [preview]="featured.preview"
              alt=""
              sizes="448px"
              [priority]="true"
            />
          </app-sheet-frame>
        }
      </div>
    </section>

    @let catalogo = data();
    @if (catalogoResource.error()) {
      <p role="alert" class="mx-auto max-w-6xl px-4 py-14 text-ink-muted">
        No pudimos cargar el catálogo. Intenta de nuevo en unos minutos.
      </p>
    } @else if (!catalogo) {
      <p class="mx-auto max-w-6xl px-4 py-14 text-ink-muted" aria-live="polite">
        Cargando catálogo…
      </p>
    } @else {
      @if (catalogo.bundles.length > 0) {
        <section
          id="paquetes"
          aria-labelledby="paquetes-titulo"
          class="mx-auto max-w-6xl scroll-mt-4 px-4 pt-14"
        >
          <h2 id="paquetes-titulo" class="text-3xl font-bold text-ink">Paquetes</h2>
          <p class="mt-1 text-ink-muted">Varias plantillas juntas a un precio menor.</p>
          <ul class="mt-6 grid gap-6 lg:grid-cols-2">
            @for (bundle of catalogo.bundles; track bundle.slug) {
              <li><app-bundle-card [bundle]="bundle" /></li>
            }
          </ul>
        </section>
      }
      <section
        id="plantillas"
        aria-labelledby="plantillas-titulo"
        class="mx-auto max-w-6xl scroll-mt-4 px-4 pt-14"
      >
        <h2 id="plantillas-titulo" class="text-3xl font-bold text-ink">Plantillas</h2>
        <p class="mt-1 text-ink-muted">
          Cada una incluye el archivo .xlsx y una copia de Google Sheets.
        </p>
        @if (catalogo.templates.length === 0) {
          <p class="mt-4 text-ink-muted">Pronto tendremos plantillas disponibles.</p>
        }
        <ul class="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          @for (template of catalogo.templates; track template.slug) {
            <li><app-template-card [template]="template" /></li>
          }
        </ul>
      </section>
    }

    <app-faq />
  `,
})
export class Home {
  protected readonly columns = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
  protected readonly catalogoResource = httpResource<Catalogo>(() => '/api/catalogo');
  protected readonly data = computed(() =>
    this.catalogoResource.hasValue() ? this.catalogoResource.value() : undefined,
  );
  /** La primera Plantilla con preview, para la hoja del hero en escritorio. */
  protected readonly featured = computed(() => {
    const template = this.data()?.templates.find((t) => t.mainPreview);
    return template?.mainPreview ? { slug: template.slug, preview: template.mainPreview } : null;
  });

  constructor() {
    inject(PageMeta).set({
      description:
        'Plantillas de presupuesto para Excel y Google Sheets. Compra una vez, descarga al instante y organiza tus finanzas.',
    });
  }

  protected scrollTo(id: string, event: Event): void {
    event.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
  }
}
