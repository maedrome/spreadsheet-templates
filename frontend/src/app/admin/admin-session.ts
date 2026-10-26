import { HttpClient } from '@angular/common/http';
import { Service, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type SessionStatus =
  'anonymous' | 'requiresTwoFactor' | 'requiresEnrollment' | 'authenticated';

export interface Session {
  status: SessionStatus;
  email: string | null;
}

export interface Enrollment {
  sharedKey: string;
  otpauthUri: string;
  qrCodeDataUrl: string;
}

/** La pantalla a la que lleva cada estado de la sesión. */
export const SESSION_HOME: Record<SessionStatus, string> = {
  anonymous: '/admin/login',
  requiresTwoFactor: '/admin/verificar',
  requiresEnrollment: '/admin/configurar-2fa',
  authenticated: '/admin/plantillas',
};

const AUTH = '/api/admin/auth';

/**
 * Sesión del administrador. El token XSRF depende del usuario, así que después de cada cambio
 * (login, 2FA, logout) se vuelve a pedir `/session`, que lo renueva.
 */
@Service()
export class AdminSession {
  private readonly http = inject(HttpClient);
  private readonly current = signal<Session>({ status: 'anonymous', email: null });

  readonly status = computed(() => this.current().status);
  readonly email = computed(() => this.current().email);

  async refresh(): Promise<Session> {
    const session = await firstValueFrom(this.http.get<Session>(`${AUTH}/session`));
    this.current.set(session);
    return session;
  }

  async login(email: string, password: string): Promise<SessionStatus> {
    await firstValueFrom(this.http.post<Session>(`${AUTH}/login`, { email, password }));
    return (await this.refresh()).status;
  }

  async verify(code: string, isRecoveryCode: boolean): Promise<SessionStatus> {
    await firstValueFrom(this.http.post<Session>(`${AUTH}/2fa/verify`, { code, isRecoveryCode }));
    return (await this.refresh()).status;
  }

  enrollment(): Promise<Enrollment> {
    return firstValueFrom(this.http.get<Enrollment>(`${AUTH}/2fa/setup`));
  }

  /** Activa el 2FA y devuelve los códigos de recuperación, que solo se muestran esta vez. */
  async enable(code: string): Promise<string[]> {
    const { recoveryCodes } = await firstValueFrom(
      this.http.post<{ recoveryCodes: string[] }>(`${AUTH}/2fa/enable`, { code }),
    );
    await this.refresh();
    return recoveryCodes;
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.http.post(`${AUTH}/logout`, null));
    await this.refresh();
  }
}
