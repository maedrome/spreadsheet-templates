import { Component, inject, input, output, signal } from '@angular/core';
import { AdminApi, Preview, TemplateDetail } from '../admin-api';
import { problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

const MAX_BYTES = 10 * 1024 * 1024;

/** Subida, orden y borrado de las Previews de una Plantilla. La primera es la Preview principal. */
@Component({
  selector: 'app-template-previews',
  template: `
    <section [class]="ui.card" aria-labelledby="previews-title">
      <h2 id="previews-title" class="mb-1 text-lg font-semibold">Previews</h2>
      <p class="mb-4" [class]="ui.hint">
        PNG, JPEG o WebP de hasta 10 MB y al menos 1600 px de ancho. Se convierten a WebP en 3
        tamaños. La primera es la Preview principal.
      </p>

      @if (template().previews.length > 0) {
        <ol class="mb-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          @for (
            preview of template().previews;
            track preview.id;
            let index = $index, first = $first, last = $last
          ) {
            <li class="rounded border border-line p-2">
              <img
                [src]="thumbnail(preview)"
                [alt]="'Preview ' + (index + 1) + ' de ' + template().name"
                width="480"
                [attr.height]="thumbnailHeight(preview)"
                class="mb-2 aspect-[16/10] w-full rounded bg-canvas object-cover"
              />
              <div class="flex flex-wrap items-center gap-2">
                @if (first) {
                  <span [class]="ui.published">Principal</span>
                }
                <span class="mr-auto text-sm text-ink-subtle">{{ index + 1 }}</span>
                <button
                  type="button"
                  class="px-2 py-1"
                  [class]="ui.secondary"
                  [disabled]="busy() || first"
                  [attr.aria-label]="'Mover la Preview ' + (index + 1) + ' antes'"
                  (click)="move(index, -1)"
                >
                  ↑
                </button>
                <button
                  type="button"
                  class="px-2 py-1"
                  [class]="ui.secondary"
                  [disabled]="busy() || last"
                  [attr.aria-label]="'Mover la Preview ' + (index + 1) + ' después'"
                  (click)="move(index, 1)"
                >
                  ↓
                </button>
                <button
                  type="button"
                  class="px-2 py-1"
                  [class]="ui.danger"
                  [disabled]="busy()"
                  [attr.aria-label]="'Borrar la Preview ' + (index + 1)"
                  (click)="remove(preview)"
                >
                  Borrar
                </button>
              </div>
            </li>
          }
        </ol>
      } @else {
        <p class="mb-4 text-sm text-ink-subtle">Todavía no hay Previews.</p>
      }

      <label [class]="ui.label" for="preview-file">Agregar Preview</label>
      <input
        id="preview-file"
        type="file"
        accept="image/png,image/jpeg,image/webp"
        [class]="ui.file"
        [disabled]="busy()"
        (change)="upload($event)"
      />
      <div aria-live="polite">
        @if (busy()) {
          <p [class]="ui.hint">Procesando…</p>
        }
        @if (error()) {
          <p role="alert" [class]="ui.error">{{ error() }}</p>
        }
      </div>
    </section>
  `,
})
export class TemplatePreviews {
  protected readonly ui = ui;
  private readonly api = inject(AdminApi);

  readonly template = input.required<TemplateDetail>();
  readonly changed = output<TemplateDetail>();

  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected thumbnail(preview: Preview): string {
    return preview.variants[0]?.url ?? '';
  }

  protected thumbnailHeight(preview: Preview): number | null {
    return preview.variants[0]?.height ?? null;
  }

  protected async upload(event: Event): Promise<void> {
    const field = event.target as HTMLInputElement;
    const file = field.files?.[0];
    if (!file) return;
    if (file.size > MAX_BYTES) {
      this.error.set('La imagen pesa más de 10 MB.');
      field.value = '';
      return;
    }
    await this.run(() => this.api.uploadPreview(this.template().id, file));
    field.value = '';
  }

  protected move(index: number, offset: -1 | 1): Promise<void> {
    const ids = this.template().previews.map((p) => p.id);
    [ids[index], ids[index + offset]] = [ids[index + offset], ids[index]];
    return this.run(() => this.api.reorderPreviews(this.template().id, ids));
  }

  protected remove(preview: Preview): Promise<void> {
    if (!confirm('¿Borrar esta Preview?')) return Promise.resolve();
    return this.run(() => this.api.deletePreview(this.template().id, preview.id));
  }

  private async run(action: () => Promise<TemplateDetail>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      this.changed.emit(await action());
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
