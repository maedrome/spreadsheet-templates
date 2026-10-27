import { Component, DestroyRef, inject, signal } from '@angular/core';
import { Meta } from '@angular/platform-browser';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminSession } from '../admin-session';
import { problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

/** Layout propio del panel, sin el header ni el footer públicos. Nunca se indexa. */
@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  host: { class: 'block min-h-screen bg-canvas text-ink' },
  template: `
    <header class="border-b border-line bg-surface">
      <div class="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3">
        <span class="flex items-center gap-2 font-display font-bold">
          <span aria-hidden="true" class="size-4 rounded-chip bg-accent"></span>
          Panel de administración
        </span>
        @if (session.status() === 'authenticated') {
          <nav aria-label="Secciones del panel" class="flex gap-1">
            @for (link of links; track link.path) {
              <a
                [routerLink]="link.path"
                routerLinkActive="font-semibold"
                ariaCurrentWhenActive="page"
                class="rounded-cell px-3 py-1.5 text-sm font-medium text-ink-muted not-aria-[current=page]:hover:bg-canvas aria-[current=page]:bg-night aria-[current=page]:text-night-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                >{{ link.label }}</a
              >
            }
          </nav>
          <div class="ml-auto flex items-center gap-3 text-sm">
            <span class="text-ink-subtle">{{ session.email() }}</span>
            <button type="button" [class]="ui.secondary" (click)="logout()">Cerrar sesión</button>
          </div>
        }
      </div>
      @if (error()) {
        <p role="alert" class="mx-auto max-w-6xl px-4 pb-3 text-sm text-danger">{{ error() }}</p>
      }
    </header>
    <main class="mx-auto max-w-6xl px-4 py-8">
      <router-outlet />
    </main>
  `,
})
export class AdminLayout {
  protected readonly ui = ui;
  protected readonly session = inject(AdminSession);
  private readonly router = inject(Router);
  protected readonly error = signal('');

  protected readonly links = [
    { path: '/admin/plantillas', label: 'Plantillas' },
    { path: '/admin/paquetes', label: 'Paquetes' },
    { path: '/admin/compras', label: 'Compras' },
  ];

  constructor() {
    const meta = inject(Meta);
    meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
    inject(DestroyRef).onDestroy(() => meta.removeTag('name="robots"'));
  }

  protected async logout(): Promise<void> {
    this.error.set('');
    try {
      await this.session.logout();
      await this.router.navigateByUrl('/admin/login');
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }
}
