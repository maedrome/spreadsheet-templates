import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BundleDetail } from '../admin-api';
import { formatUsd } from '../shared/format';
import { provideAdminTesting, query, settle, text, type } from '../testing';
import { BundleEdit } from './bundle-edit';

const BUNDLE: BundleDetail = {
  id: 3,
  slug: 'hogar',
  name: 'Hogar',
  description: 'Dos plantillas.',
  priceCents: 800,
  position: 0,
  isPublished: false,
  slugLocked: false,
  templates: [
    { id: 1, slug: 'mensual', name: 'Mensual', priceCents: 500, isPublished: true },
    { id: 2, slug: 'anual', name: 'Anual', priceCents: 500, isPublished: true },
  ],
  templatesTotalCents: 1000,
  savingsCents: 200,
  publicationProblems: [],
};

describe('BundleEdit', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideAdminTesting() });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render() {
    const fixture = TestBed.createComponent(BundleEdit);
    fixture.componentRef.setInput('id', '3');
    await settle(fixture);
    http.expectOne('/api/admin/paquetes/3').flush(BUNDLE);
    http.expectOne('/api/admin/plantillas').flush([]);
    await settle(fixture);
    return fixture;
  }

  it('calcula el Ahorro con los precios actuales de las Plantillas', async () => {
    const fixture = await render();

    expect(query(fixture, '[data-testid=savings]').textContent?.replace(/\s+/g, ' ').trim()).toBe(
      `${formatUsd(200)} (20 %)`,
    );
  });

  it('recalcula el Ahorro al cambiar el precio antes de guardar', async () => {
    const fixture = await render();

    type(fixture, '#bundle-price', '12');
    await settle(fixture);

    expect(query(fixture, '[data-testid=savings]').textContent).toContain(formatUsd(-200));
    expect(text(fixture)).toContain('El precio tiene que ser menor que la suma de las Plantillas.');
    expect(text(fixture)).toContain('Guarda los cambios antes de publicar.');
  });

  it('guarda el precio en centavos', async () => {
    const fixture = await render();
    type(fixture, '#bundle-price', '7.49');

    query(fixture, 'form').dispatchEvent(new Event('submit'));
    await settle(fixture);
    const request = http.expectOne('/api/admin/paquetes/3');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Hogar',
      slug: 'hogar',
      description: 'Dos plantillas.',
      priceCents: 749,
      position: 0,
    });
    request.flush({ ...BUNDLE, priceCents: 749, savingsCents: 251 });
    await settle(fixture);

    expect(text(fixture)).toContain('Cambios guardados.');
  });
});
