import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { AdminSession, SESSION_HOME } from '../admin-session';
import { FieldErrors } from '../shared/field-errors';
import { problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

@Component({
  selector: 'app-admin-two-factor-verify',
  imports: [FormRoot, FormField, FieldErrors],
  template: `
    <div class="mx-auto max-w-sm" [class]="ui.card">
      <h1 class="mb-2 text-xl font-semibold">Verificación en dos pasos</h1>
      <p class="mb-6 text-sm text-ink-subtle">
        @if (useRecoveryCode()) {
          Escribe uno de tus códigos de recuperación. Cada código sirve una sola vez.
        } @else {
          Escribe el código de 6 dígitos que muestra tu app de autenticación.
        }
      </p>
      <form [formRoot]="form" class="space-y-4">
        <div>
          <label [class]="ui.label" for="verify-code">{{
            useRecoveryCode() ? 'Código de recuperación' : 'Código'
          }}</label>
          <input
            id="verify-code"
            type="text"
            class="font-mono tracking-widest"
            [class]="ui.input"
            [attr.inputmode]="useRecoveryCode() ? 'text' : 'numeric'"
            autocomplete="one-time-code"
            [formField]="form.code"
            [attr.aria-invalid]="form.code().touched() && form.code().invalid()"
            aria-describedby="verify-code-errors"
          />
          <app-field-errors id="verify-code-errors" [field]="form.code" />
        </div>
        <label class="flex items-center gap-2 text-sm">
          <input type="checkbox" class="size-4" [formField]="form.useRecoveryCode" />
          Usar un código de recuperación
        </label>
        @if (error()) {
          <p role="alert" class="text-sm text-danger">{{ error() }}</p>
        }
        <button type="submit" class="w-full" [class]="ui.primary" [disabled]="form().submitting()">
          Verificar
        </button>
      </form>
    </div>
  `,
})
export class TwoFactorVerify {
  protected readonly ui = ui;
  private readonly session = inject(AdminSession);
  private readonly router = inject(Router);

  protected readonly error = signal('');
  private readonly model = signal({ code: '', useRecoveryCode: false });
  protected readonly useRecoveryCode = computed(() => this.model().useRecoveryCode);

  protected readonly form = form(
    this.model,
    (path) => required(path.code, { message: 'Escribe el código.' }),
    {
      submission: {
        action: async () => {
          this.error.set('');
          const { code, useRecoveryCode } = this.model();
          try {
            const status = await this.session.verify(code, useRecoveryCode);
            await this.router.navigateByUrl(SESSION_HOME[status]);
          } catch (error) {
            // Si la cookie del paso intermedio venció, hay que volver a poner la contraseña.
            if (
              error instanceof HttpErrorResponse &&
              error.status === 401 &&
              (await this.session.refresh()).status === 'anonymous'
            ) {
              await this.router.navigateByUrl(SESSION_HOME.anonymous);
              return undefined;
            }
            this.error.set(problemMessage(error));
          }
          return undefined;
        },
      },
    },
  );
}
