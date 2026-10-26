import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TemplateDetail } from '../admin-api';
import { provideAdminTesting, query, settle, text, type } from '../testing';
import { TemplateEdit } from './template-edit';

const SAVED: TemplateDetail = {
  id: 7,
  slug: 'presupuesto-anual',
  name: 'Presupuesto Anual',
  description: 'Todo el año.',
  priceCents: 1250,
  position: 2,
  isPublished: true,
  slugLocked: true,
  hasXlsx: true,
  googleSheetsCopyUrl: 'https://docs.google.com/spreadsheets/d/abc/copy',
  previews: [],
  bundles: [],
  missingForPublication: [],
};

describe('TemplateEdit', () => {
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideAdminTesting() });
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  async function render(id?: string) {
    const fixture = TestBed.createComponent(TemplateEdit);
    if (id) fixture.componentRef.setInput('id', id);
    await settle(fixture);
    return fixture;
  }

  function submit(fixture: Awaited<ReturnType<typeof render>>) {
    query(fixture, 'form').dispatchEvent(new Event('submit'));
  }

  it('crea una Plantilla con el slug sugerido y el precio en centavos', async () => {
    const fixture = await render();
    type(fixture, '#template-name', 'Presupuesto Anual Ñandú');
    await settle(fixture);
    expect(query<HTMLInputElement>(fixture, '#template-slug').value).toBe(
      'presupuesto-anual-nandu',
    );
    type(fixture, '#template-description', '  Todo el año.  ');
    type(fixture, '#template-price', '12.5');

    submit(fixture);
    await settle(fixture);
    const request = http.expectOne('/api/admin/plantillas');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Presupuesto Anual Ñandú',
      slug: 'presupuesto-anual-nandu',
      description: 'Todo el año.',
      priceCents: 1250,
      position: null,
      googleSheetsCopyUrl: null,
    });
    request.flush({ ...SAVED, isPublished: false, slugLocked: false });
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith(['/admin/plantillas', 7], { replaceUrl: true });
  });

  it('no envía un link de Google Sheets que no termina en /copy', async () => {
    const fixture = await render();
    type(fixture, '#template-name', 'Mensual');
    type(fixture, '#template-price', '5');
    type(fixture, '#template-sheets', 'https://docs.google.com/spreadsheets/d/abc/edit');

    submit(fixture);
    await settle(fixture);

    http.expectNone('/api/admin/plantillas');
    expect(text(fixture)).toContain(
      'El link debe tener la forma https://docs.google.com/spreadsheets/d/{id}/copy.',
    );
  });

  it('muestra los errores de validación del servidor en su campo', async () => {
    const fixture = await render();
    type(fixture, '#template-name', 'Mensual');
    type(fixture, '#template-price', '5');

    submit(fixture);
    await settle(fixture);
    http
      .expectOne('/api/admin/plantillas')
      .flush(
        { errors: { slug: ['Ya existe una Plantilla con ese slug.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle(fixture);

    expect(query(fixture, '#template-slug-errors').textContent).toContain(
      'Ya existe una Plantilla con ese slug.',
    );
    expect(navigate).not.toHaveBeenCalled();
  });

  it('carga una Plantilla publicada con el slug bloqueado', async () => {
    const fixture = await render('7');
    http.expectOne('/api/admin/plantillas/7').flush(SAVED);
    await settle(fixture);

    expect(query<HTMLInputElement>(fixture, '#template-price').value).toBe('12.5');
    expect(query<HTMLInputElement>(fixture, '#template-slug').readOnly).toBe(true);
    expect(text(fixture)).toContain('Despublicar');
  });

  it('muestra el mensaje de la regla cuando la API rechaza despublicar', async () => {
    const fixture = await render('7');
    http.expectOne('/api/admin/plantillas/7').flush(SAVED);
    await settle(fixture);

    const button = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (b) => b.textContent?.trim() === 'Despublicar',
    )!;
    button.click();
    await settle(fixture);
    http
      .expectOne('/api/admin/plantillas/7/despublicar')
      .flush(
        {
          detail: 'No se puede despublicar la Plantilla porque está en Paquetes publicados: Hogar.',
        },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle(fixture);

    expect(query(fixture, '[role=alert]').textContent).toContain(
      'está en Paquetes publicados: Hogar.',
    );
  });
});
