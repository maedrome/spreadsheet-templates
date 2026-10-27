/** Clases de Tailwind compartidas por las pantallas del panel. */

const button =
  'inline-flex items-center justify-center gap-2 rounded-cell px-4 py-2 text-sm font-medium ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ' +
  'disabled:cursor-not-allowed disabled:opacity-60';

const badge = 'inline-block rounded-chip px-2 py-0.5 text-xs font-medium';

export const ui = {
  primary: `${button} bg-accent text-on-accent hover:bg-accent-strong`,
  secondary: `${button} border border-line-strong bg-surface text-ink hover:bg-canvas`,
  danger: `${button} border border-danger-line bg-surface text-danger hover:bg-danger-soft`,
  label: 'mb-1 block text-sm font-medium text-ink',
  input:
    'block w-full rounded-cell border border-line-strong bg-surface px-3 py-2 text-ink ' +
    'focus:border-accent focus:outline-2 focus:outline-accent read-only:bg-canvas aria-invalid:border-danger',
  error: 'mt-1 text-sm text-danger',
  hint: 'mt-1 text-sm text-ink-subtle',
  card: 'rounded-cell border border-line bg-surface p-6',
  published: `${badge} bg-accent-soft text-accent-ink`,
  draft: `${badge} bg-sunken text-ink`,
  table:
    'w-full border-collapse text-left text-sm ' +
    '[&_th]:border-b [&_th]:border-line [&_th]:bg-canvas [&_th]:px-3 [&_th]:py-2 [&_th]:font-semibold [&_th]:text-ink-muted ' +
    '[&_td]:border-b [&_td]:border-line [&_td]:px-3 [&_td]:py-2 [&_td]:align-middle',
  link: 'font-medium text-ink underline underline-offset-2 hover:text-accent-ink',
  file: 'block text-sm file:mr-3 file:rounded-cell file:border-0 file:bg-night file:px-3 file:py-2 file:text-night-ink',
} as const;
