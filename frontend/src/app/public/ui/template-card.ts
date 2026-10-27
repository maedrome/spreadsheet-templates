import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TemplateCard } from '../catalogo.models';
import { PreviewImage } from './preview-image';
import { PricePipe } from './price.pipe';
import { SheetFrame } from './sheet-frame';

@Component({
  selector: 'app-template-card',
  imports: [RouterLink, PreviewImage, PricePipe, SheetFrame],
  host: { class: 'block h-full' },
  template: `
    <a
      [routerLink]="['/plantillas', template().slug]"
      class="group flex h-full flex-col overflow-hidden rounded-cell border border-line bg-surface transition hover:border-accent hover:shadow-lift"
    >
      @if (template().mainPreview; as preview) {
        <app-sheet-frame class="border-b border-line" [file]="template().slug + '.xlsx'">
          <app-preview-image
            [preview]="preview"
            [alt]="'Vista previa de ' + template().name"
            sizes="(min-width: 1024px) 384px, (min-width: 640px) 50vw, 100vw"
          />
        </app-sheet-frame>
      }
      <div class="flex flex-1 flex-col gap-2 p-4">
        <h3 class="text-lg font-bold text-ink group-hover:text-accent-ink">
          {{ template().name }}
        </h3>
        <p class="line-clamp-3 text-sm text-ink-muted">{{ template().description }}</p>
        <p class="mt-auto flex items-center justify-between gap-3 pt-2">
          <span class="font-mono text-lg font-bold text-ink">{{
            template().priceCents | price
          }}</span>
          <span class="font-mono text-xs text-ink-subtle">XLSX · SHEETS</span>
        </p>
      </div>
    </a>
  `,
})
export class TemplateCardView {
  readonly template = input.required<TemplateCard>();
}
