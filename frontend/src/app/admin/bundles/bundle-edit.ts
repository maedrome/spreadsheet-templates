import { httpResource } from '@angular/common/http';
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
import { ADMIN_API, AdminApi, BundleDetail, BundleInput, TemplateListItem } from '../admin-api';
import { FieldErrors } from '../shared/field-errors';
import {
  SLUG_PATTERN,
  UsdPipe,
  fieldErrors,
  problemMessage,
  slugify,
  toCents,
} from '../shared/format';
import { serverErrors } from '../shared/server-errors';
import { ui } from '../shared/ui';

interface BundleFormModel {
  name: string;
  slug: string;
  description: string;
  price: number | null;
  position: number | null;
}

const EMPTY: BundleFormModel = { name: '', slug: '', description: '', price: null, position: null };

@Component({
  selector: 'app-admin-bundle-edit',
  imports: [FormRoot, FormField, FieldErrors, RouterLink, UsdPipe],
  templateUrl: './bundle-edit.html',
})
export class BundleEdit {
  protected readonly ui = ui;
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);

  /** Parámetro de la ruta; no viene al crear un Paquete nuevo. */
  readonly id = input<string>();

  protected readonly bundle = signal<BundleDetail | null>(null);
  protected readonly loadError = signal('');
  protected readonly actionError = signal('');
  protected readonly notice = signal('');
  protected readonly busy = signal(false);
  protected readonly isNew = computed(() => !this.id());

  private readonly allTemplates = httpResource<TemplateListItem[]>(() => `${ADMIN_API}/plantillas`);
  /** Las Plantillas que todavía no están en el Paquete. */
  protected readonly available = computed(() => {
    const included = new Set(this.bundle()?.templates.map((t) => t.id));
    return (this.allTemplates.value() ?? []).filter((t) => !included.has(t.id));
  });
  protected readonly selectedTemplateId = signal('');

  private readonly model = signal<BundleFormModel>(EMPTY);

  /** Ahorro con el precio que está escrito en el formulario, aunque todavía no se haya guardado. */
  protected readonly templatesTotalCents = computed(() => this.bundle()?.templatesTotalCents ?? 0);
  protected readonly priceCents = computed(() => toCents(this.model().price ?? 0));
  protected readonly savingsCents = computed(() => this.templatesTotalCents() - this.priceCents());
  protected readonly savingsPercent = computed(() =>
    this.templatesTotalCents() > 0
      ? Math.round((this.savingsCents() / this.templatesTotalCents()) * 100)
      : 0,
  );

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.name, { message: 'El nombre es obligatorio.' });
      maxLength(path.name, 200, { message: 'El nombre no puede pasar de 200 caracteres.' });
      required(path.slug, { message: 'El slug es obligatorio.' });
      pattern(path.slug, SLUG_PATTERN, { message: 'Usa solo minúsculas, números y guiones.' });
      readonly(path.slug, () => this.bundle()?.slugLocked ?? false);
      required(path.price, { message: 'El precio es obligatorio.' });
      min(path.price, 0, { message: 'El precio no puede ser negativo.' });
      min(path.position, 0, { message: 'La posición no puede ser negativa.' });
    },
    { submission: { action: () => this.save() } },
  );

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) untracked(() => void this.load(Number(id)));
    });
  }

  protected suggestSlug(): void {
    if (this.isNew() && !this.model().slug) this.form.slug().value.set(slugify(this.model().name));
  }

  protected async addTemplate(): Promise<void> {
    const bundle = this.bundle();
    const templateId = Number(this.selectedTemplateId());
    if (!bundle || !templateId) return;
    await this.run(async () => {
      this.bundle.set(await this.api.addTemplateToBundle(bundle.id, templateId));
      this.selectedTemplateId.set('');
    });
  }

  protected async removeTemplate(templateId: number): Promise<void> {
    const bundle = this.bundle();
    if (!bundle) return;
    await this.run(async () =>
      this.bundle.set(await this.api.removeTemplateFromBundle(bundle.id, templateId)),
    );
  }

  protected async setPublished(published: boolean): Promise<void> {
    const bundle = this.bundle();
    if (!bundle) return;
    await this.run(async () => {
      this.bundle.set(await this.api.setBundlePublished(bundle.id, published));
      this.notice.set(published ? 'Paquete publicado.' : 'Paquete despublicado.');
    });
  }

  protected async remove(): Promise<void> {
    const bundle = this.bundle();
    if (!bundle || !confirm(`¿Borrar el Paquete "${bundle.name}"? No se puede deshacer.`)) return;
    await this.run(async () => {
      await this.api.deleteBundle(bundle.id);
      await this.router.navigateByUrl('/admin/paquetes');
    });
  }

  protected selectTemplate(event: Event): void {
    this.selectedTemplateId.set((event.target as HTMLSelectElement).value);
  }

  private async load(id: number): Promise<void> {
    this.loadError.set('');
    try {
      this.showSaved(await this.api.getBundle(id));
    } catch (error) {
      this.loadError.set(problemMessage(error));
    }
  }

  private async save(): Promise<TreeValidationResult> {
    this.actionError.set('');
    this.notice.set('');
    const model = this.model();
    const input: BundleInput = {
      name: model.name.trim(),
      slug: model.slug.trim(),
      description: model.description.trim(),
      priceCents: toCents(model.price ?? 0),
      position: model.position,
    };
    const current = this.bundle();
    try {
      if (current) {
        this.showSaved(await this.api.updateBundle(current.id, input));
        this.notice.set('Cambios guardados.');
      } else {
        const created = await this.api.createBundle(input);
        await this.router.navigate(['/admin/paquetes', created.id], { replaceUrl: true });
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
          },
          errors,
        );
      }
      this.actionError.set(problemMessage(error));
      return undefined;
    }
  }

  private showSaved(bundle: BundleDetail): void {
    this.bundle.set(bundle);
    this.model.set({
      name: bundle.name,
      slug: bundle.slug,
      description: bundle.description,
      price: bundle.priceCents / 100,
      position: bundle.position,
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
