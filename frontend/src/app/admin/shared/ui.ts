/** Clases de Tailwind compartidas por las pantallas del panel. */

const button =
  'inline-flex items-center justify-center gap-2 rounded px-4 py-2 text-sm font-medium ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900 ' +
  'disabled:cursor-not-allowed disabled:opacity-60';

const badge = 'inline-block rounded-full px-2 py-0.5 text-xs font-medium';

export const ui = {
  primary: `${button} bg-slate-900 text-white hover:bg-slate-700`,
  secondary: `${button} border border-slate-300 bg-white text-slate-800 hover:bg-slate-100`,
  danger: `${button} border border-red-300 bg-white text-red-700 hover:bg-red-50`,
  label: 'mb-1 block text-sm font-medium text-slate-800',
  input:
    'block w-full rounded border border-slate-400 bg-white px-3 py-2 text-slate-900 ' +
    'focus:border-slate-900 focus:outline-2 focus:outline-slate-900 read-only:bg-slate-100 aria-invalid:border-red-700',
  error: 'mt-1 text-sm text-red-700',
  hint: 'mt-1 text-sm text-slate-600',
  card: 'rounded-lg border border-slate-200 bg-white p-6 shadow-sm',
  published: `${badge} bg-green-100 text-green-800`,
  draft: `${badge} bg-slate-200 text-slate-800`,
  table:
    'w-full border-collapse text-left text-sm ' +
    '[&_th]:border-b [&_th]:border-slate-300 [&_th]:px-3 [&_th]:py-2 [&_th]:font-semibold [&_th]:text-slate-700 ' +
    '[&_td]:border-b [&_td]:border-slate-200 [&_td]:px-3 [&_td]:py-2 [&_td]:align-middle',
  link: 'font-medium text-slate-900 underline underline-offset-2 hover:text-slate-600',
  file: 'block text-sm file:mr-3 file:rounded file:border-0 file:bg-slate-900 file:px-3 file:py-2 file:text-white',
} as const;
