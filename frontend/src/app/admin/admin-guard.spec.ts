import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { sessionGuard } from './admin-guard';
import { AdminSession, SessionStatus } from './admin-session';
import { provideAdminTesting } from './testing';

describe('sessionGuard', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideAdminTesting() });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function run(required: SessionStatus, current: SessionStatus) {
    const result = TestBed.runInInjectionContext(() =>
      sessionGuard(required)({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Promise<boolean | UrlTree>;
    http.expectOne('/api/admin/auth/session').flush({ status: current, email: null });
    return result;
  }

  function url(result: boolean | UrlTree): string {
    return TestBed.inject(Router).serializeUrl(result as UrlTree);
  }

  it('deja pasar cuando la sesión está en el estado pedido', async () => {
    expect(await run('authenticated', 'authenticated')).toBe(true);
    expect(TestBed.inject(AdminSession).status()).toBe('authenticated');
  });

  it('lleva al login sin sesión', async () => {
    expect(url(await run('authenticated', 'anonymous'))).toBe('/admin/login');
  });

  it('lleva a la verificación cuando falta el segundo factor', async () => {
    expect(url(await run('authenticated', 'requiresTwoFactor'))).toBe('/admin/verificar');
  });

  it('obliga a configurar el 2FA en el primer login', async () => {
    expect(url(await run('authenticated', 'requiresEnrollment'))).toBe('/admin/configurar-2fa');
  });

  it('saca del login a quien ya entró', async () => {
    expect(url(await run('anonymous', 'authenticated'))).toBe('/admin/plantillas');
  });

  it('trata un error de red como sesión anónima', async () => {
    const result = TestBed.runInInjectionContext(() =>
      sessionGuard('authenticated')({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Promise<boolean | UrlTree>;
    http.expectOne('/api/admin/auth/session').error(new ProgressEvent('error'));

    expect(url(await result)).toBe('/admin/login');
  });
});
