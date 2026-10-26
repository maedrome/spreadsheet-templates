import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { STORE_NAME } from './store';

@Component({
  selector: 'app-public-layout',
  imports: [RouterLink, RouterOutlet],
  host: { class: 'flex min-h-screen flex-col' },
  template: `
    <a
      href="#contenido"
      class="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-10 focus:rounded focus:bg-white focus:px-3 focus:py-2"
      (click)="skipToContent($event)"
    >
      Saltar al contenido
    </a>
    <header class="border-b border-line bg-surface">
      <div class="mx-auto flex max-w-6xl items-center justify-between px-4 py-4">
        <a routerLink="/" class="text-lg font-bold tracking-tight text-ink">{{ storeName }}</a>
        <nav aria-label="Principal" class="flex gap-5 text-sm font-medium text-ink-muted">
          <a routerLink="/" fragment="paquetes" class="hover:text-accent">Paquetes</a>
          <a routerLink="/" fragment="plantillas" class="hover:text-accent">Plantillas</a>
        </nav>
      </div>
    </header>
    <main id="contenido" tabindex="-1" class="flex-1 outline-none">
      <router-outlet />
    </main>
    <footer class="border-t border-line bg-sunken">
      <div
        class="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-8 text-sm text-ink-muted sm:flex-row sm:items-center sm:justify-between"
      >
        <p>© {{ storeName }}. Precios en USD.</p>
        <nav aria-label="Legal" class="flex flex-wrap gap-x-5 gap-y-2">
          <a routerLink="/terminos" class="hover:text-accent">Términos</a>
          <a routerLink="/privacidad" class="hover:text-accent">Privacidad</a>
          <a routerLink="/recuperar" class="hover:text-accent">Recuperar mi compra</a>
        </nav>
      </div>
    </footer>
  `,
})
export class PublicLayout {
  protected readonly storeName = inject(STORE_NAME);

  protected skipToContent(event: Event): void {
    event.preventDefault();
    document.getElementById('contenido')?.focus();
  }
}
