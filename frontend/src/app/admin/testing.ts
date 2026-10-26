import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

/** Providers comunes de los tests del panel: HttpClient de prueba y un router vacío. */
export function provideAdminTesting() {
  return [provideHttpClient(), provideHttpClientTesting(), provideRouter([])];
}

/** Deja correr las promesas pendientes y vuelve a renderizar. */
export async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let i = 0; i < 3; i++) {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
  }
}

export function type(fixture: ComponentFixture<unknown>, selector: string, value: string): void {
  const element = query<HTMLInputElement>(fixture, selector);
  element.value = value;
  element.dispatchEvent(new Event('input'));
  element.dispatchEvent(new Event('blur'));
}

export function query<T extends Element = HTMLElement>(
  fixture: ComponentFixture<unknown>,
  selector: string,
): T {
  const element = (fixture.nativeElement as HTMLElement).querySelector<T>(selector);
  if (!element) throw new Error(`No se encontró ${selector}`);
  return element;
}

export function text(fixture: ComponentFixture<unknown>): string {
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}
