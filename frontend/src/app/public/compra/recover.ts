import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, email, form, required } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { PageMeta } from '../page-meta';

export const RECOVERY_MESSAGE = 'Si hay compras con ese email, te enviamos los enlaces.';

/** Pide los enlaces de las compras de un email. La respuesta es siempre la misma, exista o no el email. */
@Component({
  selector: 'app-recover',
  imports: [FormRoot, FormField],
  template: `
    <section class="mx-auto max-w-md px-4 py-12">
      <h1 class="text-4xl font-extrabold text-ink">Recuperar mi compra</h1>
      <p class="mt-3 text-ink-muted">
        ¿Perdiste el email de tu compra? Escribe el email con el que pagaste y te enviamos un enlace
        nuevo por cada compra. Los enlaces anteriores siguen funcionando.
      </p>

      @if (sent()) {
        <p
          role="status"
          class="mt-8 rounded-cell border border-accent bg-accent-soft p-4 text-accent-ink"
        >
          {{ message }}
        </p>
      } @else {
        <form [formRoot]="form" class="mt-8 space-y-4">
          <div>
            <label for="recover-email" class="block text-sm font-semibold text-ink">Email</label>
            <input
              id="recover-email"
              type="email"
              autocomplete="email"
              inputmode="email"
              class="mt-1 block w-full rounded-cell border border-line-strong bg-surface px-4 py-3 text-ink aria-invalid:border-danger"
              [formField]="form.email"
              [attr.aria-invalid]="form.email().touched() && form.email().invalid()"
              aria-describedby="recover-email-errors"
            />
            <div id="recover-email-errors" aria-live="polite">
              @if (form.email().touched()) {
                @for (error of form.email().errors(); track $index) {
                  <p class="mt-1 text-sm text-danger">{{ error.message }}</p>
                }
              }
            </div>
          </div>
          @if (failed()) {
            <p role="alert" class="text-sm text-danger">{{ failed() }}</p>
          }
          <button
            type="submit"
            class="btn btn-primary w-full text-lg"
            [disabled]="form().submitting()"
          >
            {{ form().submitting() ? 'Enviando…' : 'Enviarme los enlaces' }}
          </button>
        </form>
      }
    </section>
  `,
})
export class RecoverPage {
  private readonly http = inject(HttpClient);

  protected readonly message = RECOVERY_MESSAGE;
  protected readonly sent = signal(false);
  protected readonly failed = signal('');
  private readonly model = signal({ email: '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.email, { message: 'Escribe tu email.' });
      email(path.email, { message: 'El email no es válido.' });
    },
    {
      submission: {
        action: async () => {
          this.failed.set('');
          try {
            await firstValueFrom(this.http.post('/api/recuperar', { email: this.model().email }));
            this.sent.set(true);
          } catch (error) {
            this.failed.set(
              error instanceof HttpErrorResponse && error.status === 429
                ? 'Hiciste demasiados intentos. Inténtalo de nuevo más tarde.'
                : 'No pudimos enviar la solicitud. Inténtalo de nuevo en unos minutos.',
            );
          }
          return undefined;
        },
      },
    },
  );

  constructor() {
    inject(PageMeta).set({
      title: 'Recuperar mi compra',
      description: 'Recupera los enlaces de descarga de tus compras.',
      noindex: true,
    });
  }
}
