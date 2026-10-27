import { httpResource } from '@angular/common/http';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ADMIN_API, BundleListItem } from '../admin-api';
import { UsdPipe, problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

@Component({
  selector: 'app-admin-bundle-list',
  imports: [RouterLink, UsdPipe],
  template: `
    <div class="mb-6 flex items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold">Paquetes</h1>
      <a routerLink="/admin/paquetes/nuevo" [class]="ui.primary">Nuevo Paquete</a>
    </div>

    @if (bundles.error(); as error) {
      <p role="alert" class="text-danger">{{ message(error) }}</p>
    } @else if (bundles.hasValue()) {
      @if (bundles.value().length === 0) {
        <p class="text-ink-subtle">Todavía no hay Paquetes.</p>
      } @else {
        <div class="overflow-x-auto rounded-cell border border-line bg-surface">
          <table [class]="ui.table">
            <caption class="sr-only">
              Paquetes en el orden en que aparecen en la tienda
            </caption>
            <thead>
              <tr>
                <th scope="col">Posición</th>
                <th scope="col">Nombre</th>
                <th scope="col">Plantillas</th>
                <th scope="col">Precio</th>
                <th scope="col">Ahorro</th>
                <th scope="col">Estado</th>
              </tr>
            </thead>
            <tbody>
              @for (bundle of bundles.value(); track bundle.id) {
                <tr>
                  <td>{{ bundle.position }}</td>
                  <td>
                    <a [class]="ui.link" [routerLink]="['/admin/paquetes', bundle.id]">{{
                      bundle.name
                    }}</a>
                    <span class="block text-xs text-ink-subtle">{{ bundle.slug }}</span>
                  </td>
                  <td>{{ bundle.templateCount }}</td>
                  <td>{{ bundle.priceCents | usd }}</td>
                  <td [class]="bundle.savingsCents > 0 ? '' : 'text-danger'">
                    {{ bundle.savingsCents | usd }}
                  </td>
                  <td>
                    <span [class]="bundle.isPublished ? ui.published : ui.draft">
                      {{ bundle.isPublished ? 'Publicado' : 'Sin publicar' }}
                    </span>
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
export class BundleList {
  protected readonly ui = ui;
  protected readonly bundles = httpResource<BundleListItem[]>(() => `${ADMIN_API}/paquetes`);
  protected readonly message = problemMessage;
}
