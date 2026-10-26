import { HttpErrorResponse } from '@angular/common/http';
import { Pipe, PipeTransform } from '@angular/core';

const usd = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' });
const dateTime = new Intl.DateTimeFormat('es-EC', { dateStyle: 'medium', timeStyle: 'short' });

export function formatUsd(cents: number): string {
  return usd.format(cents / 100);
}

@Pipe({ name: 'usd' })
export class UsdPipe implements PipeTransform {
  transform(cents: number): string {
    return formatUsd(cents);
  }
}

@Pipe({ name: 'dateTime' })
export class DateTimePipe implements PipeTransform {
  transform(value: string): string {
    return dateTime.format(new Date(value));
  }
}

/** Dólares escritos por el admin (p. ej. 9.99) a centavos. */
export function toCents(dollars: number): number {
  return Math.round(dollars * 100);
}

export function slugify(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

export const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

interface Problem {
  detail?: string;
  title?: string;
  errors?: Record<string, string[]>;
}

/** El mensaje de un ProblemDetails de la API, listo para mostrar. */
export function problemMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'Ocurrió un error inesperado.';
  if (error.status === 0) return 'No se pudo conectar con el servidor.';
  const problem = error.error as Problem | null;
  if (problem?.errors) return Object.values(problem.errors).flat().join(' ');
  if (problem?.detail) return problem.detail;
  if (error.status === 401) return 'Tu sesión expiró. Vuelve a iniciar sesión.';
  if (error.status === 403) return 'Termina de configurar la verificación en dos pasos.';
  return problem?.title ?? 'Ocurrió un error inesperado.';
}

/** Los errores por campo de un ValidationProblemDetails (400), o `null` si no lo es. */
export function fieldErrors(error: unknown): Record<string, string[]> | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) return null;
  return (error.error as Problem | null)?.errors ?? null;
}
