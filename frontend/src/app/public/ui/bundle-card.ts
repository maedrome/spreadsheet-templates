import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BundleCard } from '../catalogo.models';
import { PreviewImage } from './preview-image';
import { PricePipe } from './price.pipe';
import { SavingsTag } from './savings-tag';

@Component({
  selector: 'app-bundle-card',
  imports: [RouterLink, PreviewImage, PricePipe, SavingsTag],
  host: { class: '@container block h-full' },
  template: `
    <a
      [routerLink]="['/paquetes', bundle().slug]"
      class="group flex h-full flex-col overflow-hidden rounded-cell border border-accent bg-surface transition hover:shadow-lift @lg:flex-row"
    >
      <ul
        class="grid grid-cols-3 content-start gap-px border-b border-line bg-line @lg:w-1/2 @lg:shrink-0 @lg:grid-cols-2 @lg:border-r @lg:border-b-0 @lg:[&>li:first-child]:col-span-2"
      >
        @for (template of bundle().templates; track template.slug) {
          <li class="bg-surface">
            @if (template.mainPreview; as preview) {
              <app-preview-image
                [preview]="preview"
                [alt]="'Vista previa de ' + template.name"
                sizes="(min-width: 1024px) 280px, 30vw"
              />
            }
          </li>
        }
      </ul>
      <div class="flex flex-1 flex-col gap-2 p-4">
        <app-savings-tag [cents]="bundle().savingsCents" />
        <h3 class="text-lg font-bold text-ink group-hover:text-accent-ink">{{ bundle().name }}</h3>
        <p class="line-clamp-3 text-sm text-ink-muted">{{ bundle().description }}</p>
        <p class="mt-auto flex items-center justify-between gap-3 pt-2">
          <span class="font-mono text-lg font-bold text-ink">{{
            bundle().priceCents | price
          }}</span>
          <span class="font-mono text-xs text-ink-subtle"
            >{{ bundle().templates.length }} plantillas</span
          >
        </p>
      </div>
    </a>
  `,
})
export class BundleCardView {
  readonly bundle = input.required<BundleCard>();
}
