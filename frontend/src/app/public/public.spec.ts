import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Meta, Title } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { BundleDetailPage } from './bundle-detail';
import { Catalogo, Preview } from './catalogo.models';
import { Home } from './home';
import { NotFound } from './not-found';
import { TemplateDetailPage } from './template-detail';
import { PreviewImage } from './ui/preview-image';
import { PricePipe } from './ui/price.pipe';

const preview = (name: string): Preview => ({
  variants: [480, 960, 1600].map((width) => ({
    url: `/api/previews/${name}-${width}.webp`,
    width,
    height: width / 2,
  })),
});

async function render<T>(component: Type<T>, inputs: Record<string, unknown> = {}) {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  const fixture = TestBed.createComponent(component);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  return { fixture, http: TestBed.inject(HttpTestingController) };
}

async function respond(fixture: ComponentFixture<unknown>, flush: () => void) {
  flush();
  await fixture.whenStable();
  fixture.detectChanges();
}

const text = (fixture: ComponentFixture<unknown>) =>
  (fixture.nativeElement as HTMLElement).textContent ?? '';

describe('PricePipe', () => {
  it('formats cents as USD', () => {
    expect(new PricePipe().transform(1250)).toMatch(/12[,.]50/);
    expect(new PricePipe().transform(1250)).toContain('$');
  });
});

describe('PreviewImage', () => {
  it('builds a srcset from the variants with dimensions and lazy loading', async () => {
    const { fixture } = await render(PreviewImage, { preview: preview('a'), alt: 'Vista' });
    const img = (fixture.nativeElement as HTMLElement).querySelector('img')!;

    expect(img.getAttribute('srcset')).toBe(
      '/api/previews/a-480.webp 480w, /api/previews/a-960.webp 960w, /api/previews/a-1600.webp 1600w',
    );
    expect(img.getAttribute('width')).toBe('960');
    expect(img.getAttribute('height')).toBe('480');
    expect(img.getAttribute('loading')).toBe('lazy');
    expect(img.getAttribute('alt')).toBe('Vista');
  });
});

describe('Home', () => {
  const catalogo: Catalogo = {
    bundles: [
      {
        slug: 'paquete',
        name: 'Paquete completo',
        description: 'Todo junto',
        priceCents: 2900,
        savingsCents: 700,
        templates: [{ slug: 'uno', name: 'Uno', priceCents: 1000, mainPreview: preview('uno') }],
      },
    ],
    templates: [
      {
        slug: 'uno',
        name: 'Uno',
        description: 'Primera',
        priceCents: 1000,
        mainPreview: preview('uno'),
      },
      { slug: 'dos', name: 'Dos', description: 'Segunda', priceCents: 1500, mainPreview: null },
    ],
  };

  it('shows bundles, templates and the FAQ', async () => {
    const { fixture, http } = await render(Home);
    await respond(fixture, () => http.expectOne('/api/catalogo').flush(catalogo));

    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelectorAll('app-bundle-card')).toHaveLength(1);
    expect(html.querySelectorAll('app-template-card')).toHaveLength(2);
    expect(text(fixture)).toContain('Ahorras');
    expect(text(fixture)).toContain('reembolso');
    expect(html.querySelector('a[href="/plantillas/uno"]')).toBeTruthy();
    expect(TestBed.inject(Title).getTitle()).toBeTruthy();
  });

  it('shows an error message when the catalog fails', async () => {
    const { fixture, http } = await render(Home);
    await respond(fixture, () =>
      http.expectOne('/api/catalogo').flush('', { status: 500, statusText: 'Server Error' }),
    );

    expect(text(fixture)).toContain('No pudimos cargar el catálogo');
  });
});

describe('TemplateDetailPage', () => {
  it('shows the gallery, the price and a disabled buy button', async () => {
    const { fixture, http } = await render(TemplateDetailPage, { slug: 'uno' });
    await respond(fixture, () =>
      http.expectOne('/api/plantillas/uno').flush({
        slug: 'uno',
        name: 'Plantilla Uno',
        description: 'Una descripción',
        priceCents: 1000,
        previews: [preview('a'), preview('b')],
      }),
    );

    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('h1')?.textContent).toContain('Plantilla Uno');
    expect(html.querySelectorAll('img').length).toBe(3);
    expect(html.querySelector('button[disabled]')?.textContent).toContain('Comprar');
    expect(TestBed.inject(Title).getTitle()).toContain('Plantilla Uno');
    expect(TestBed.inject(Meta).getTag("name='description'")?.content).toBe('Una descripción');
  });

  it('renders the 404 page when the template does not exist', async () => {
    const { fixture, http } = await render(TemplateDetailPage, { slug: 'nada' });
    await respond(fixture, () =>
      http.expectOne('/api/plantillas/nada').flush('', { status: 404, statusText: 'Not Found' }),
    );

    expect(text(fixture)).toContain('No encontramos esta página');
    expect(TestBed.inject(Meta).getTag("name='robots'")?.content).toBe('noindex');
  });
});

describe('BundleDetailPage', () => {
  it('shows the savings and the included templates', async () => {
    const { fixture, http } = await render(BundleDetailPage, { slug: 'paquete' });
    await respond(fixture, () =>
      http.expectOne('/api/paquetes/paquete').flush({
        slug: 'paquete',
        name: 'Paquete completo',
        description: 'Todo junto',
        priceCents: 2900,
        savingsCents: 700,
        templates: [
          { slug: 'uno', name: 'Uno', priceCents: 1000, mainPreview: preview('uno') },
          { slug: 'dos', name: 'Dos', priceCents: 1500, mainPreview: preview('dos') },
        ],
      }),
    );

    const html = fixture.nativeElement as HTMLElement;
    expect(text(fixture)).toMatch(/Ahorras\s+\$\s?7[,.]00/);
    expect(html.querySelectorAll('li a')).toHaveLength(2);
    expect(html.querySelector('button[disabled]')).toBeTruthy();
  });
});

describe('NotFound', () => {
  it('links back home', async () => {
    const { fixture } = await render(NotFound);

    expect((fixture.nativeElement as HTMLElement).querySelector('a[href="/"]')).toBeTruthy();
  });
});
