import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { Catalogo } from './catalogo.models';
import { Faq } from './faq';
import { PageMeta } from './page-meta';
import { BundleCardView } from './ui/bundle-card';
import { TemplateCardView } from './ui/template-card';

@Component({
  selector: 'app-home',
  imports: [BundleCardView, TemplateCardView, Faq],
  template: `
    <section class="bg-sunken">
      <div class="mx-auto max-w-6xl px-4 py-14 sm:py-20">
        <h1 class="max-w-2xl text-4xl font-bold tracking-tight text-ink sm:text-5xl">
          Ordena tu dinero con plantillas de presupuesto listas para usar
        </h1>
        <p class="mt-4 max-w-xl text-lg text-ink-muted">
          Plantillas para Excel y Google Sheets. Las compras una vez, las descargas al instante y
          son tuyas para siempre.
        </p>
        <a
          href="#plantillas"
          class="mt-8 inline-block rounded-lg bg-accent px-5 py-3 font-semibold text-white hover:bg-accent-strong"
          (click)="scrollTo('plantillas', $event)"
        >
          Ver plantillas
        </a>
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
          <h2 id="paquetes-titulo" class="text-2xl font-bold text-ink">Paquetes</h2>
          <p class="mt-1 text-ink-muted">Varias plantillas juntas a un precio menor.</p>
          <ul class="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
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
        <h2 id="plantillas-titulo" class="text-2xl font-bold text-ink">Plantillas</h2>
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
  protected readonly catalogoResource = httpResource<Catalogo>(() => '/api/catalogo');
  protected readonly data = computed(() =>
    this.catalogoResource.hasValue() ? this.catalogoResource.value() : undefined,
  );

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
