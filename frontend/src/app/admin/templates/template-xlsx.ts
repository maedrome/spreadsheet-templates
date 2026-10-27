import { Component, inject, input, output, signal } from '@angular/core';
import { AdminApi, TemplateDetail } from '../admin-api';
import { problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

const MAX_BYTES = 20 * 1024 * 1024;

/** El archivo .xlsx que se entrega con la Compra. Subir uno nuevo reemplaza al anterior. */
@Component({
  selector: 'app-template-xlsx',
  template: `
    <section [class]="ui.card" aria-labelledby="xlsx-title">
      <h2 id="xlsx-title" class="mb-1 text-lg font-semibold">Archivo Excel</h2>
      <p class="mb-4 text-sm" [class]="template().hasXlsx ? 'text-accent-ink' : 'text-ink-subtle'">
        {{
          template().hasXlsx
            ? 'Ya hay un .xlsx cargado. Subir otro lo reemplaza.'
            : 'Todavía no hay un .xlsx.'
        }}
      </p>
      <label [class]="ui.label" for="xlsx-file">{{
        template().hasXlsx ? 'Reemplazar .xlsx' : 'Subir .xlsx'
      }}</label>
      <input
        id="xlsx-file"
        type="file"
        accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        [class]="ui.file"
        aria-describedby="xlsx-hint"
        [disabled]="busy()"
        (change)="upload($event)"
      />
      <p id="xlsx-hint" [class]="ui.hint">Hasta 20 MB.</p>
      <div aria-live="polite">
        @if (busy()) {
          <p [class]="ui.hint">Subiendo…</p>
        }
        @if (error()) {
          <p role="alert" [class]="ui.error">{{ error() }}</p>
        }
      </div>
    </section>
  `,
})
export class TemplateXlsx {
  protected readonly ui = ui;
  private readonly api = inject(AdminApi);

  readonly template = input.required<TemplateDetail>();
  readonly changed = output<TemplateDetail>();

  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected async upload(event: Event): Promise<void> {
    const field = event.target as HTMLInputElement;
    const file = field.files?.[0];
    if (!file) return;
    this.error.set('');
    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      this.error.set('El archivo debe tener la extensión .xlsx.');
    } else if (file.size > MAX_BYTES) {
      this.error.set('El archivo .xlsx pesa más de 20 MB.');
    } else {
      this.busy.set(true);
      try {
        this.changed.emit(await this.api.uploadXlsx(this.template().id, file));
      } catch (error) {
        this.error.set(problemMessage(error));
      } finally {
        this.busy.set(false);
      }
    }
    field.value = '';
  }
}
