import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Meta, Title } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { Faq } from '../faq';
import { LEGAL_INFO } from './legal-info';
import { PrivacyPage } from './privacy';
import { TermsPage } from './terms';

function render(component: Type<unknown>) {
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('páginas legales', () => {
  it('/terminos toma los datos de legal-info y cubre reembolso y Polar', () => {
    const html = render(TermsPage);
    const text = html.textContent ?? '';

    expect(text).toContain(LEGAL_INFO.ownerName);
    expect(text).toContain(LEGAL_INFO.ruc);
    expect(text).toContain(LEGAL_INFO.contactEmail);
    expect(text).toContain('Merchant of Record');
    expect(text).toContain(`${LEGAL_INFO.refundDays} días`);
    expect(html.querySelector('a[href="/recuperar"]')).toBeTruthy();
    expect(TestBed.inject(Title).getTitle()).toContain('Términos');
    expect(TestBed.inject(Meta).getTag("name='description'")).toBeTruthy();
  });

  it('/privacidad toma los datos de legal-info y nombra a los encargados', () => {
    const html = render(PrivacyPage);
    const text = html.textContent ?? '';

    expect(text).toContain(LEGAL_INFO.ownerName);
    expect(text).toContain(LEGAL_INFO.ruc);
    expect(text).toContain(LEGAL_INFO.contactEmail);
    for (const name of ['Polar.sh', 'Resend', 'Hetzner', 'Umami']) expect(text).toContain(name);
    expect(html.querySelector(`a[href="mailto:${LEGAL_INFO.contactEmail}"]`)).toBeTruthy();
    expect(TestBed.inject(Title).getTitle()).toContain('privacidad');
  });

  it('muestra el aviso de borrador en desarrollo', () => {
    expect(render(TermsPage).querySelector('[data-testid="draft-note"]')).toBeTruthy();
  });

  it('las preguntas frecuentes mencionan el reembolso de 14 días y la recuperación', () => {
    TestBed.configureTestingModule({});
    const fixture = TestBed.createComponent(Faq);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('14 días');
    expect(text).toContain('Recuperar mi compra');
  });
});
