import { FieldTree, TreeValidationResult } from '@angular/forms/signals';

/**
 * Los errores de un ValidationProblemDetails, en el campo del formulario que les corresponde.
 * `fields` traduce el nombre del campo en la API (p. ej. `priceCents`) a su campo en el formulario.
 */
export function serverErrors(
  root: FieldTree<unknown>,
  fields: Record<string, FieldTree<unknown>>,
  errors: Record<string, string[]>,
): TreeValidationResult {
  return Object.entries(errors).flatMap(([key, messages]) =>
    messages.map((message) => ({ kind: 'server', message, fieldTree: fields[key] ?? root })),
  );
}
