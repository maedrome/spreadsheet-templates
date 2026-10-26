import { inject, Service } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { STORE_NAME } from './store';

/** Fija el título y las metas de cada ruta desde el cliente (ADR 0002). */
@Service()
export class PageMeta {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly storeName = inject(STORE_NAME);

  set(options: { title?: string; description: string; noindex?: boolean }): void {
    const title = options.title ? `${options.title} | ${this.storeName}` : this.storeName;
    this.title.setTitle(title);
    this.meta.updateTag({ name: 'description', content: options.description });
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: options.description });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    if (options.noindex) {
      this.meta.updateTag({ name: 'robots', content: 'noindex' });
    } else {
      this.meta.removeTag("name='robots'");
    }
  }
}
