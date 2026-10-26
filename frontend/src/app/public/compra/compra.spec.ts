import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Meta } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { BuyButton } from '../ui/buy-button';
import { EXTERNAL_REDIRECT } from './checkout';
import { CheckoutStatus, Purchase } from './compra.models';
import { ConfirmingPage, MAX_POLLS, POLL_INTERVAL_MS } from './confirming';
import { PurchasePage } from './purchase';

function render<T>(component: Type<T>, inputs: Record<string, unknown>) {
  const redirect = vi.fn<(url: string) => void>();
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: EXTERNAL_REDIRECT, useValue: redirect },
    ],
  });
  const fixture = TestBed.createComponent(component);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  return { fixture, http: TestBed.inject(HttpTestingController), redirect };
}

async function settle(fixture: ComponentFixture<unknown>) {
  await fixture.whenStable();
  fixture.detectChanges();
}

const text = (fixture: ComponentFixture<unknown>) =>
  (fixture.nativeElement as HTMLElement).textContent ?? '';

describe('BuyButton', () => {
  it('creates the checkout and redirects to Polar', async () => {
    const { fixture, http, redirect } = render(BuyButton, { tipo: 'paquete', slug: 'hogar' });
    const button = (fixture.nativeElement as HTMLElement).querySelector('button')!;

    button.click();
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    const request = http.expectOne('/api/checkout');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ tipo: 'paquete', slug: 'hogar' });
    request.flush({ url: 'https://sandbox.polar.sh/checkout/abc' });
    await settle(fixture);

    expect(redirect).toHaveBeenCalledWith('https://sandbox.polar.sh/checkout/abc');
  });

  it('shows an error and re-enables the button when the checkout fails', async () => {
    const { fixture, http, redirect } = render(BuyButton, { tipo: 'plantilla', slug: 'uno' });
    const button = (fixture.nativeElement as HTMLElement).querySelector('button')!;

    button.click();
    http.expectOne('/api/checkout').flush('', { status: 502, statusText: 'Bad Gateway' });
    await settle(fixture);

    expect(redirect).not.toHaveBeenCalled();
    expect(button.disabled).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).toBeTruthy();
  });
});

describe('ConfirmingPage', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  const status = (value: CheckoutStatus) => value;

  it('polls until the order exists and then goes to the purchase page', async () => {
    const { fixture, http } = render(ConfirmingPage, { checkout: 'c1' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    expect(text(fixture)).toContain('Confirmando tu pago...');
    expect(TestBed.inject(Meta).getTag("name='robots'")?.content).toBe('noindex');

    http.expectOne('/api/checkout/c1/estado').flush(status({ status: 'pending', token: null }));
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    http.expectOne('/api/checkout/c1/estado').flush(status({ status: 'paid', token: 'tok' }));
    await vi.advanceTimersByTimeAsync(0);

    expect(navigate).toHaveBeenCalledWith(['/compra', 'tok'], { replaceUrl: true });
  });

  it('asks to check the email when the token was already issued', async () => {
    const { fixture, http } = render(ConfirmingPage, { checkout: 'c2' });

    http.expectOne('/api/checkout/c2/estado').flush(status({ status: 'paid', token: null }));
    await vi.advanceTimersByTimeAsync(0);
    fixture.detectChanges();

    expect(text(fixture)).toContain('Revisa tu bandeja de entrada');
  });

  it('stops after about a minute and asks to check the email', async () => {
    const { fixture, http } = render(ConfirmingPage, { checkout: 'c3' });

    for (let poll = 1; poll <= MAX_POLLS; poll++) {
      http.expectOne('/api/checkout/c3/estado').flush(status({ status: 'pending', token: null }));
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    }
    fixture.detectChanges();

    http.expectNone('/api/checkout/c3/estado');
    expect(MAX_POLLS * POLL_INTERVAL_MS).toBe(60_000);
    expect(text(fixture)).toContain('está tardando');
  });
});

describe('PurchasePage', () => {
  const purchase: Purchase = {
    productType: 'paquete',
    productName: 'Paquete hogar',
    email: 'cliente@example.com',
    pricePaidCents: 1900,
    paidAt: '2026-10-25T12:00:00Z',
    isRefunded: false,
    templates: [
      { id: 1, slug: 'uno', name: 'Uno' },
      { id: 2, slug: 'dos', name: 'Dos' },
    ],
  };

  it('shows the purchase summary without indexing', async () => {
    const { fixture, http } = render(PurchasePage, { token: 'tok' });
    http.expectOne('/api/compras/tok').flush(purchase);
    await settle(fixture);

    expect(text(fixture)).toContain('Paquete hogar');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('li a')).toHaveLength(2);
    expect(text(fixture)).not.toContain('reembolsada');
    expect(TestBed.inject(Meta).getTag("name='robots'")?.content).toBe('noindex');
  });

  it('says when the purchase was refunded', async () => {
    const { fixture, http } = render(PurchasePage, { token: 'tok' });
    http.expectOne('/api/compras/tok').flush({ ...purchase, isRefunded: true });
    await settle(fixture);

    expect(text(fixture)).toContain('fue reembolsada');
  });

  it('says when the link is not valid', async () => {
    const { fixture, http } = render(PurchasePage, { token: 'malo' });
    http.expectOne('/api/compras/malo').flush('', { status: 404, statusText: 'Not Found' });
    await settle(fixture);

    expect(text(fixture)).toContain('Este enlace no es válido');
  });
});
