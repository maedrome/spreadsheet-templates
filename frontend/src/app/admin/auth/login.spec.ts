import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideAdminTesting, query, settle, text, type } from '../testing';
import { Login } from './login';

describe('Login', () => {
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideAdminTesting() });
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  async function render() {
    const fixture = TestBed.createComponent(Login);
    await settle(fixture);
    return fixture;
  }

  function submit(fixture: Awaited<ReturnType<typeof render>>) {
    query(fixture, 'form').dispatchEvent(new Event('submit'));
  }

  it('no envía nada con el formulario vacío y muestra qué falta', async () => {
    const fixture = await render();

    submit(fixture);
    await settle(fixture);

    http.expectNone('/api/admin/auth/login');
    expect(text(fixture)).toContain('Escribe tu email.');
    expect(text(fixture)).toContain('Escribe tu contraseña.');
    expect(query(fixture, '#login-email').getAttribute('aria-invalid')).toBe('true');
  });

  it('inicia sesión y va a la pantalla del nuevo estado', async () => {
    const fixture = await render();
    type(fixture, '#login-email', 'duena@example.com');
    type(fixture, '#login-password', 'Contraseña-Segura-123');

    submit(fixture);
    await settle(fixture);
    const login = http.expectOne('/api/admin/auth/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.body).toEqual({
      email: 'duena@example.com',
      password: 'Contraseña-Segura-123',
    });
    login.flush({ status: 'requiresTwoFactor', email: null });
    await settle(fixture);
    http.expectOne('/api/admin/auth/session').flush({ status: 'requiresTwoFactor', email: null });
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith('/admin/verificar');
  });

  it('muestra el error de la API', async () => {
    const fixture = await render();
    type(fixture, '#login-email', 'duena@example.com');
    type(fixture, '#login-password', 'incorrecta');

    submit(fixture);
    await settle(fixture);
    http
      .expectOne('/api/admin/auth/login')
      .flush(
        { detail: 'Email o contraseña incorrectos.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    await settle(fixture);

    expect(query(fixture, '[role=alert]').textContent).toContain('Email o contraseña incorrectos.');
    expect(navigate).not.toHaveBeenCalled();
  });
});
