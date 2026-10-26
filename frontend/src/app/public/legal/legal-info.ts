/**
 * Fuente única de los datos del vendedor para las páginas legales.
 *
 * BORRADOR: los textos de /terminos y /privacidad los redactó una IA y deben
 * revisarse con un abogado antes de publicarse. Todo valor entre corchetes
 * "[COMPLETAR: ...]" es un placeholder que el titular debe reemplazar por su
 * dato real; no se inventó ningún dato del vendedor.
 */
export const LEGAL_INFO = {
  /** Nombre comercial de la tienda. */
  storeName: '[COMPLETAR: nombre de la tienda]',
  /** Nombre legal del titular (persona natural con RUC en Ecuador). */
  ownerName: '[COMPLETAR: nombre legal del titular]',
  /** Registro Único de Contribuyentes del titular. */
  ruc: '[COMPLETAR: RUC]',
  /** Email de contacto para soporte, reembolsos y derechos sobre datos personales. */
  contactEmail: '[COMPLETAR: email de contacto]',
  /** Ciudad del titular. */
  city: '[COMPLETAR: ciudad]',
  country: 'Ecuador',
  /** Dominio del sitio, sin protocolo. */
  domain: '[COMPLETAR: dominio]',
  /** Fecha de última actualización de los textos legales. */
  lastUpdated: '[COMPLETAR: fecha de última actualización]',
  /** Días de la garantía de reembolso total. */
  refundDays: 14,
} as const;

/** Verdadero mientras algún dato sigue siendo un placeholder. */
export const hasPlaceholders = Object.values(LEGAL_INFO).some(
  (value) => typeof value === 'string' && value.startsWith('[COMPLETAR'),
);
