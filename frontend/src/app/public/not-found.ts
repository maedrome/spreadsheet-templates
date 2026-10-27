import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageMeta } from './page-meta';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <section class="mx-auto max-w-2xl px-4 py-20 text-center">
      <p class="mx-auto w-fit rounded-chip bg-danger-soft px-2 py-1 font-mono text-xs text-danger">
        #¡REF! · Error 404
      </p>
      <h1 class="mt-4 text-4xl font-extrabold text-ink">No encontramos esta página</h1>
      <p class="mt-3 text-ink-muted">
        Puede que el enlace esté mal escrito o que el producto ya no esté disponible.
      </p>
      <a routerLink="/" class="btn btn-primary mt-8"> Volver al inicio </a>
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
