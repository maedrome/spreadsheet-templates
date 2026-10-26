import { Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, pattern, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { AdminSession, Enrollment, SESSION_HOME } from '../admin-session';
import { FieldErrors } from '../shared/field-errors';
import { problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

/** Enrolamiento obligatorio en el primer login: QR de la app de autenticación y códigos de recuperación. */
@Component({
  selector: 'app-admin-two-factor-setup',
  imports: [FormRoot, FormField, FieldErrors],
  template: `
    <div class="mx-auto max-w-lg" [class]="ui.card">
      <h1 class="mb-2 text-xl font-semibold">Configura la verificación en dos pasos</h1>

      @if (recoveryCodes(); as codes) {
        <p class="mb-4 text-sm text-slate-700">
          Listo. Guarda estos códigos de recuperación en un lugar seguro: son la única forma de
          entrar si pierdes el teléfono, y no se vuelven a mostrar. Cada uno sirve una sola vez.
        </p>
        <ol
          class="mb-6 grid grid-cols-2 gap-2 rounded bg-slate-100 p-4 font-mono text-sm"
          aria-label="Códigos de recuperación"
        >
          @for (code of codes; track code) {
            <li>{{ code }}</li>
          }
        </ol>
        <button type="button" [class]="ui.primary" (click)="continue()">
          Ya los guardé, ir al panel
        </button>
      } @else {
        <p class="mb-6 text-sm text-slate-600">
          El panel exige un segundo factor. Escanea el código con una app de autenticación (Google
          Authenticator, 1Password, Authy…) y escribe el código que genera.
        </p>
        @if (enrollment(); as setup) {
          <!-- NgOptimizedImage no admite imágenes base64. -->
          <img
            [src]="setup.qrCodeDataUrl"
            alt="Código QR para agregar la cuenta a la app de autenticación"
            width="240"
            height="240"
            class="mx-auto mb-4 size-60"
          />
          <p class="mb-6 text-center text-sm text-slate-700">
            ¿No puedes escanearlo? Escribe esta clave:
            <code class="mt-1 block font-mono text-base break-all text-slate-900">{{
              setup.sharedKey
            }}</code>
          </p>
          <form [formRoot]="form" class="space-y-4">
            <div>
              <label [class]="ui.label" for="setup-code">Código de 6 dígitos</label>
              <input
                id="setup-code"
                type="text"
                inputmode="numeric"
                autocomplete="one-time-code"
                class="font-mono tracking-widest"
                [class]="ui.input"
                [formField]="form.code"
                [attr.aria-invalid]="form.code().touched() && form.code().invalid()"
                aria-describedby="setup-code-errors"
              />
              <app-field-errors id="setup-code-errors" [field]="form.code" />
            </div>
            <button type="submit" [class]="ui.primary" [disabled]="form().submitting()">
              Activar
            </button>
          </form>
        } @else if (!error()) {
          <p class="text-sm text-slate-600">Cargando…</p>
        }
        @if (error()) {
          <p role="alert" class="mt-4 text-sm text-red-700">{{ error() }}</p>
        }
      }
    </div>
  `,
})
export class TwoFactorSetup {
  protected readonly ui = ui;
  private readonly session = inject(AdminSession);
  private readonly router = inject(Router);

  protected readonly enrollment = signal<Enrollment | null>(null);
  protected readonly recoveryCodes = signal<string[] | null>(null);
  protected readonly error = signal('');
  private readonly model = signal({ code: '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.code, { message: 'Escribe el código.' });
      pattern(path.code, /^\s*\d{3}\s?\d{3}\s*$/, { message: 'El código tiene 6 dígitos.' });
    },
    {
      submission: {
        action: async () => {
          this.error.set('');
          try {
            this.recoveryCodes.set(await this.session.enable(this.model().code));
          } catch (error) {
            this.error.set(problemMessage(error));
          }
          return undefined;
        },
      },
    },
  );

  constructor() {
    this.session.enrollment().then(
      (enrollment) => this.enrollment.set(enrollment),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }

  protected continue(): void {
    void this.router.navigateByUrl(SESSION_HOME.authenticated);
  }
}
