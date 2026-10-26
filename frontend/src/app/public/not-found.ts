import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageMeta } from './page-meta';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <section class="mx-auto max-w-2xl px-4 py-20 text-center">
      <p class="text-sm font-semibold text-accent">Error 404</p>
      <h1 class="mt-2 text-3xl font-bold text-ink">No encontramos esta página</h1>
      <p class="mt-3 text-ink-muted">
        Puede que el enlace esté mal escrito o que el producto ya no esté disponible.
      </p>
      <a
        routerLink="/"
        class="mt-8 inline-block rounded-lg bg-accent px-5 py-3 font-semibold text-white hover:bg-accent-strong"
      >
        Volver al inicio
      </a>
    </section>
  `,
})
export class NotFound {
  constructor() {
    inject(PageMeta).set({
      title: 'Página no encontrada',
      description: 'La página que buscas no existe.',
      noindex: true,
    });
  }
}
