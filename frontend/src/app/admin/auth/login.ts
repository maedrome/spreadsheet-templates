import { Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, email, form, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { AdminSession, SESSION_HOME } from '../admin-session';
import { FieldErrors } from '../shared/field-errors';
import { problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

@Component({
  selector: 'app-admin-login',
  imports: [FormRoot, FormField, FieldErrors],
  template: `
    <div class="mx-auto max-w-sm" [class]="ui.card">
      <h1 class="mb-6 text-xl font-semibold">Iniciar sesión</h1>
      <form [formRoot]="form" class="space-y-4">
        <div>
          <label [class]="ui.label" for="login-email">Email</label>
          <input
            id="login-email"
            type="email"
            autocomplete="username"
            [class]="ui.input"
            [formField]="form.email"
            [attr.aria-invalid]="form.email().touched() && form.email().invalid()"
            aria-describedby="login-email-errors"
          />
          <app-field-errors id="login-email-errors" [field]="form.email" />
        </div>
        <div>
          <label [class]="ui.label" for="login-password">Contraseña</label>
          <input
            id="login-password"
            type="password"
            autocomplete="current-password"
            [class]="ui.input"
            [formField]="form.password"
            [attr.aria-invalid]="form.password().touched() && form.password().invalid()"
            aria-describedby="login-password-errors"
          />
          <app-field-errors id="login-password-errors" [field]="form.password" />
        </div>
        @if (error()) {
          <p role="alert" class="text-sm text-danger">{{ error() }}</p>
        }
        <button type="submit" class="w-full" [class]="ui.primary" [disabled]="form().submitting()">
          Entrar
        </button>
      </form>
    </div>
  `,
})
export class Login {
  protected readonly ui = ui;
  private readonly session = inject(AdminSession);
  private readonly router = inject(Router);

  protected readonly error = signal('');
  private readonly model = signal({ email: '', password: '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.email, { message: 'Escribe tu email.' });
      email(path.email, { message: 'El email no es válido.' });
      required(path.password, { message: 'Escribe tu contraseña.' });
    },
    {
      submission: {
        action: async () => {
          this.error.set('');
          const { email, password } = this.model();
          try {
            const status = await this.session.login(email, password);
            await this.router.navigateByUrl(SESSION_HOME[status]);
          } catch (error) {
            this.error.set(problemMessage(error));
          }
          return undefined;
        },
      },
    },
  );
}
