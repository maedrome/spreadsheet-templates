import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BundleCard } from '../catalogo.models';
import { PreviewImage } from './preview-image';
import { PricePipe } from './price.pipe';

@Component({
  selector: 'app-bundle-card',
  imports: [RouterLink, PreviewImage, PricePipe],
  host: { class: 'block h-full' },
  template: `
    <a
      [routerLink]="['/paquetes', bundle().slug]"
      class="group flex h-full flex-col overflow-hidden rounded-xl border-2 border-accent/40 bg-surface transition hover:border-accent hover:shadow-md"
    >
      <ul class="grid grid-cols-3 gap-2 border-b border-line bg-sunken p-3">
        @for (template of bundle().templates; track template.slug) {
          <li class="overflow-hidden rounded border border-line bg-surface">
            @if (template.mainPreview; as preview) {
              <app-preview-image
                [preview]="preview"
                [alt]="'Vista previa de ' + template.name"
                sizes="(min-width: 640px) 128px, 30vw"
              />
            }
          </li>
        }
      </ul>
      <div class="flex flex-1 flex-col gap-2 p-4">
        <p
          class="w-fit rounded-full bg-accent-soft px-3 py-0.5 text-xs font-semibold text-accent-strong"
        >
          Ahorras {{ bundle().savingsCents | price }}
        </p>
        <h3 class="text-lg font-semibold text-ink group-hover:text-accent">{{ bundle().name }}</h3>
        <p class="line-clamp-3 text-sm text-ink-muted">{{ bundle().description }}</p>
        <p class="mt-auto pt-2 text-lg font-bold text-ink">
          {{ bundle().priceCents | price }}
          <span class="text-sm font-normal text-ink-muted"
            >· {{ bundle().templates.length }} plantillas</span
          >
        </p>
      </div>
    </a>
  `,
})
export class BundleCardView {
  readonly bundle = input.required<BundleCard>();
}
