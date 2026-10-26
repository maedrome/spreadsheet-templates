import { IMAGE_LOADER, ImageLoaderConfig, NgOptimizedImage } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { Preview } from '../catalogo.models';

/** Preview con `srcset` armado a partir de sus variantes WebP. */
@Component({
  selector: 'app-preview-image',
  imports: [NgOptimizedImage],
  host: { class: 'block' },
  providers: [
    {
      provide: IMAGE_LOADER,
      useFactory: () => {
        const self = inject(PreviewImage);
        return (config: ImageLoaderConfig) => self.urlFor(config.width) ?? config.src;
      },
    },
  ],
  template: `
    @if (image(); as img) {
      <img
        class="h-auto w-full"
        [ngSrc]="img.src"
        [ngSrcset]="img.srcset"
        [width]="img.width"
        [height]="img.height"
        [sizes]="sizes()"
        [alt]="alt()"
        [priority]="priority()"
      />
    }
  `,
})
export class PreviewImage {
  readonly preview = input.required<Preview>();
  readonly alt = input.required<string>();
  readonly sizes = input('100vw');
  readonly priority = input(false);

  protected readonly image = computed(() => {
    const variants = [...this.preview().variants].sort((a, b) => a.width - b.width);
    if (variants.length === 0) return null;
    const fallback = variants[Math.floor(variants.length / 2)];
    return {
      src: fallback.url,
      width: fallback.width,
      height: fallback.height,
      srcset: variants.map((v) => `${v.width}w`).join(', '),
    };
  });

  /** URL de la variante del ancho pedido; la usa el loader de NgOptimizedImage. */
  urlFor(width: number | undefined): string | undefined {
    return this.preview().variants.find((v) => v.width === width)?.url;
  }
}
