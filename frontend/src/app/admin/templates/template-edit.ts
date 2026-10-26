import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import {
  FormField,
  FormRoot,
  TreeValidationResult,
  form,
  maxLength,
  min,
  pattern,
  readonly,
  required,
} from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { AdminApi, TemplateDetail, TemplateInput } from '../admin-api';
import { FieldErrors } from '../shared/field-errors';
import { serverErrors } from '../shared/server-errors';
import { SLUG_PATTERN, fieldErrors, problemMessage, slugify, toCents } from '../shared/format';
import { TemplatePreviews } from './template-previews';
import { TemplateXlsx } from './template-xlsx';
import { ui } from '../shared/ui';

export const GOOGLE_SHEETS_COPY_URL =
  /^https:\/\/docs\.google\.com\/spreadsheets\/d\/[A-Za-z0-9_-]+\/copy$/;

interface TemplateFormModel {
  name: string;
  slug: string;
  description: string;
  price: number | null;
  position: number | null;
  googleSheetsCopyUrl: string;
}

const EMPTY: TemplateFormModel = {
  name: '',
  slug: '',
  description: '',
  price: null,
  position: null,
  googleSheetsCopyUrl: '',
};

@Component({
  selector: 'app-admin-template-edit',
  imports: [FormRoot, FormField, FieldErrors, RouterLink, TemplatePreviews, TemplateXlsx],
  templateUrl: './template-edit.html',
})
export class TemplateEdit {
  protected readonly ui = ui;
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);

  /** Parámetro de la ruta; no viene al crear una Plantilla nueva. */
  readonly id = input<string>();

  protected readonly template = signal<TemplateDetail | null>(null);
  protected readonly loadError = signal('');
  protected readonly actionError = signal('');
  protected readonly notice = signal('');
  protected readonly busy = signal(false);
  protected readonly isNew = computed(() => !this.id());

  private readonly model = signal<TemplateFormModel>(EMPTY);
  protected readonly form = form(
    this.model,
    (path) => {
      required(path.name, { message: 'El nombre es obligatorio.' });
      maxLength(path.name, 200, { message: 'El nombre no puede pasar de 200 caracteres.' });
      required(path.slug, { message: 'El slug es obligatorio.' });
      pattern(path.slug, SLUG_PATTERN, { message: 'Usa solo minúsculas, números y guiones.' });
      readonly(path.slug, () => this.template()?.slugLocked ?? false);
      required(path.price, { message: 'El precio es obligatorio.' });
      min(path.price, 0, { message: 'El precio no puede ser negativo.' });
      min(path.position, 0, { message: 'La posición no puede ser negativa.' });
      pattern(path.googleSheetsCopyUrl, GOOGLE_SHEETS_COPY_URL, {
        message: 'El link debe tener la forma https://docs.google.com/spreadsheets/d/{id}/copy.',
      });
    },
    { submission: { action: () => this.save() } },
  );

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) untracked(() => void this.load(Number(id)));
    });
  }

  /** Al crear, el slug se sugiere a partir del nombre mientras el admin no escriba uno. */
  protected suggestSlug(): void {
    if (this.isNew() && !this.model().slug) this.form.slug().value.set(slugify(this.model().name));
  }

  /** Previews y .xlsx actualizan la Plantilla sin tocar los cambios del formulario sin guardar. */
  protected onFilesChanged(template: TemplateDetail): void {
    this.template.set(template);
  }

  protected async setPublished(published: boolean): Promise<void> {
    const template = this.template();
    if (!template) return;
    await this.run(async () => {
      this.template.set(await this.api.setTemplatePublished(template.id, published));
      this.notice.set(published ? 'Plantilla publicada.' : 'Plantilla despublicada.');
    });
  }

  protected async remove(): Promise<void> {
    const template = this.template();
    if (!template || !confirm(`¿Borrar la Plantilla "${template.name}"? No se puede deshacer.`))
      return;
    await this.run(async () => {
      await this.api.deleteTemplate(template.id);
      await this.router.navigateByUrl('/admin/plantillas');
    });
  }

  private async load(id: number): Promise<void> {
    this.loadError.set('');
    try {
      this.showSaved(await this.api.getTemplate(id));
    } catch (error) {
      this.loadError.set(problemMessage(error));
    }
  }

  private async save(): Promise<TreeValidationResult> {
    this.actionError.set('');
    this.notice.set('');
    const input = toInput(this.model());
    const current = this.template();
    try {
      if (current) {
        this.showSaved(await this.api.updateTemplate(current.id, input));
        this.notice.set('Cambios guardados.');
      } else {
        const created = await this.api.createTemplate(input);
        await this.router.navigate(['/admin/plantillas', created.id], { replaceUrl: true });
      }
      return undefined;
    } catch (error) {
      const errors = fieldErrors(error);
      if (errors) {
        const f = this.form;
        return serverErrors(
          f,
          {
            name: f.name,
            slug: f.slug,
            description: f.description,
            priceCents: f.price,
            position: f.position,
            googleSheetsCopyUrl: f.googleSheetsCopyUrl,
          },
          errors,
        );
      }
      this.actionError.set(problemMessage(error));
      return undefined;
    }
  }

  private showSaved(template: TemplateDetail): void {
    this.template.set(template);
    this.model.set({
      name: template.name,
      slug: template.slug,
      description: template.description,
      price: template.priceCents / 100,
      position: template.position,
      googleSheetsCopyUrl: template.googleSheetsCopyUrl ?? '',
    });
    this.form().reset();
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.actionError.set('');
    this.notice.set('');
    try {
      await action();
    } catch (error) {
      this.actionError.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}

function toInput(model: TemplateFormModel): TemplateInput {
  return {
    name: model.name.trim(),
    slug: model.slug.trim(),
    description: model.description.trim(),
    priceCents: toCents(model.price ?? 0),
    position: model.position,
    googleSheetsCopyUrl: model.googleSheetsCopyUrl.trim() || null,
  };
}
