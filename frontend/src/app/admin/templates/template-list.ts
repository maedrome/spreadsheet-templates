import { httpResource } from '@angular/common/http';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ADMIN_API, TemplateListItem } from '../admin-api';
import { UsdPipe, problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

@Component({
  selector: 'app-admin-template-list',
  imports: [RouterLink, UsdPipe],
  template: `
    <div class="mb-6 flex items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold">Plantillas</h1>
      <a routerLink="/admin/plantillas/nueva" [class]="ui.primary">Nueva Plantilla</a>
    </div>

    @if (templates.error(); as error) {
      <p role="alert" class="text-danger">{{ message(error) }}</p>
    } @else if (templates.hasValue()) {
      @if (templates.value().length === 0) {
        <p class="text-ink-subtle">Todavía no hay Plantillas.</p>
      } @else {
        <div class="overflow-x-auto rounded-cell border border-line bg-surface">
          <table [class]="ui.table">
            <caption class="sr-only">
              Plantillas en el orden en que aparecen en la tienda
            </caption>
            <thead>
              <tr>
                <th scope="col">Posición</th>
                <th scope="col"><span class="sr-only">Preview principal</span></th>
                <th scope="col">Nombre</th>
                <th scope="col">Precio</th>
                <th scope="col">Estado</th>
                <th scope="col">Archivos</th>
              </tr>
            </thead>
            <tbody>
              @for (template of templates.value(); track template.id) {
                <tr>
                  <td>{{ template.position }}</td>
                  <td class="w-20">
                    @if (template.mainPreviewUrl) {
                      <img
                        [src]="template.mainPreviewUrl"
                        alt=""
                        width="64"
                        height="40"
                        class="h-10 w-16 rounded object-cover"
                      />
                    }
                  </td>
                  <td>
                    <a [class]="ui.link" [routerLink]="['/admin/plantillas', template.id]">{{
                      template.name
                    }}</a>
                    <span class="block text-xs text-ink-subtle">{{ template.slug }}</span>
                  </td>
                  <td>{{ template.priceCents | usd }}</td>
                  <td>
                    <span [class]="template.isPublished ? ui.published : ui.draft">
                      {{ template.isPublished ? 'Publicada' : 'Sin publicar' }}
                    </span>
                  </td>
                  <td class="text-ink-muted">
                    {{ template.previewCount }}
                    {{ template.previewCount === 1 ? 'Preview' : 'Previews' }} ·
                    {{ template.hasXlsx ? '.xlsx' : 'sin .xlsx' }} ·
                    {{ template.hasGoogleSheets ? 'Sheets' : 'sin Sheets' }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    } @else {
      <p class="text-ink-subtle">Cargando…</p>
    }
  `,
})
export class TemplateList {
  protected readonly ui = ui;
  protected readonly templates = httpResource<TemplateListItem[]>(() => `${ADMIN_API}/plantillas`);
  protected readonly message = problemMessage;
}
