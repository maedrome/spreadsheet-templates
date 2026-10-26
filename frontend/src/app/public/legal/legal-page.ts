import { Component, input, isDevMode } from '@angular/core';
import { LEGAL_INFO } from './legal-info';

/** Marco común de las páginas legales: título, fecha y aviso de borrador (solo en desarrollo). */
@Component({
  selector: 'app-legal-page',
  template: `
    <article class="mx-auto max-w-3xl px-4 py-12">
      @if (devMode) {
        <p
          role="note"
          class="mb-6 rounded-lg border border-line bg-sunken p-3 text-sm text-ink"
          data-testid="draft-note"
        >
          <strong>Borrador (solo visible en desarrollo):</strong> este texto debe revisarse con un
          abogado y completarse con los datos reales en <code>legal-info.ts</code> antes de
          publicarse.
        </p>
      }
      <h1 class="text-3xl font-bold tracking-tight text-ink">{{ heading() }}</h1>
      <p class="mt-2 text-sm text-ink-muted">Última actualización: {{ info.lastUpdated }}</p>
      <div class="legal-body mt-8 space-y-4 leading-relaxed text-ink-muted">
        <ng-content />
      </div>
    </article>
  `,
  styles: `
    .legal-body ::ng-deep h2 {
      margin-top: 2rem;
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--color-ink);
    }
    .legal-body ::ng-deep ul {
      list-style: disc;
      padding-left: 1.5rem;
    }
    .legal-body ::ng-deep li + li {
      margin-top: 0.5rem;
    }
    .legal-body ::ng-deep a {
      text-decoration: underline;
      color: var(--color-accent);
      overflow-wrap: anywhere;
    }
    .legal-body ::ng-deep strong {
      color: var(--color-ink);
    }
  `,
})
export class LegalPage {
  readonly heading = input.required<string>();
  protected readonly info = LEGAL_INFO;
  protected readonly devMode = isDevMode();
}
