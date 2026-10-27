import { Component, input } from '@angular/core';

/** Marco de hoja de cálculo para una preview: barra con la pestaña del archivo y el contenido debajo. */
@Component({
  selector: 'app-sheet-frame',
  host: { class: 'block overflow-hidden bg-surface' },
  template: `
    <div
      aria-hidden="true"
      class="flex items-end gap-1 border-b border-line bg-canvas px-2 pt-1.5 font-mono text-[11px] leading-none text-ink-subtle"
    >
      <span
        class="-mb-px truncate rounded-t-chip border border-line border-b-surface bg-surface px-2 py-1.5 text-accent-ink"
        >{{ file() }}</span
      >
      <span class="px-1.5 py-1.5">+</span>
    </div>
    <ng-content />
  `,
})
export class SheetFrame {
  /** El texto de la pestaña, p. ej. el nombre del archivo. */
  readonly file = input.required<string>();
}
