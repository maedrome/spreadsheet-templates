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
      class="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-10 focus:rounded-cell focus:bg-surface focus:px-3 focus:py-2 focus:text-ink"
      (click)="skipToContent($event)"
    >
      Saltar al contenido
    </a>
    <header class="bg-night text-night-ink">
      <div
        class="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-6 gap-y-2 px-4 py-3.5"
      >
        <a
          routerLink="/"
          class="flex items-center gap-2.5 font-display text-lg font-extrabold tracking-tight focus-visible:outline-mint"
        >
          <span aria-hidden="true" class="size-5 shrink-0 rounded-chip bg-accent"></span>
          {{ storeName }}
        </a>
        <nav
          aria-label="Principal"
          class="flex shrink-0 gap-5 text-sm font-medium text-night-muted"
        >
          <a routerLink="/" fragment="paquetes" class="hover:text-mint focus-visible:outline-mint"
            >Paquetes</a
          >
          <a routerLink="/" fragment="plantillas" class="hover:text-mint focus-visible:outline-mint"
            >Plantillas</a
          >
        </nav>
      </div>
    </header>
    <main id="contenido" tabindex="-1" class="flex-1 outline-none">
      <router-outlet />
    </main>
    <footer class="border-t border-line bg-surface">
      <div
        class="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-8 text-sm text-ink-muted sm:flex-row sm:items-center sm:justify-between"
      >
        <p>
          © {{ storeName }}.
          <span class="font-mono text-xs text-ink-subtle">Precios en USD</span>
        </p>
        <nav aria-label="Legal" class="flex flex-wrap gap-x-5 gap-y-2">
          <a routerLink="/terminos" class="hover:text-accent hover:underline">Términos</a>
          <a routerLink="/privacidad" class="hover:text-accent hover:underline">Privacidad</a>
          <a routerLink="/recuperar" class="hover:text-accent hover:underline"
            >Recuperar mi compra</a
          >
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
