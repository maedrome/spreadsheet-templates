import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TemplateCard } from '../catalogo.models';
import { PreviewImage } from './preview-image';
import { PricePipe } from './price.pipe';

@Component({
  selector: 'app-template-card',
  imports: [RouterLink, PreviewImage, PricePipe],
  host: { class: 'block h-full' },
  template: `
    <a
      [routerLink]="['/plantillas', template().slug]"
      class="group flex h-full flex-col overflow-hidden rounded-xl border border-line bg-surface transition hover:border-accent hover:shadow-md"
    >
      @if (template().mainPreview; as preview) {
        <app-preview-image
          class="border-b border-line bg-sunken"
          [preview]="preview"
          [alt]="'Vista previa de ' + template().name"
          sizes="(min-width: 1024px) 384px, (min-width: 640px) 50vw, 100vw"
        />
      }
      <div class="flex flex-1 flex-col gap-2 p-4">
        <h3 class="text-lg font-semibold text-ink group-hover:text-accent">
          {{ template().name }}
        </h3>
        <p class="line-clamp-3 text-sm text-ink-muted">{{ template().description }}</p>
        <p class="mt-auto pt-2 text-lg font-bold text-ink">{{ template().priceCents | price }}</p>
      </div>
    </a>
  `,
})
export class TemplateCardView {
  readonly template = input.required<TemplateCard>();
}
