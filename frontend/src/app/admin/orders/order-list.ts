import { httpResource } from '@angular/common/http';
import { Component } from '@angular/core';
import { ADMIN_API, OrderListItem } from '../admin-api';
import { DateTimePipe, UsdPipe, problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

/** Compras de solo lectura. El reenvío del Enlace de compra llega con los emails, en la fase 4. */
@Component({
  selector: 'app-admin-order-list',
  imports: [UsdPipe, DateTimePipe],
  template: `
    <h1 class="mb-6 text-2xl font-semibold">Compras</h1>

    @if (orders.error(); as error) {
      <p role="alert" class="text-red-700">{{ message(error) }}</p>
    } @else if (orders.hasValue()) {
      @if (orders.value().length === 0) {
        <p class="text-slate-600">Todavía no hay Compras.</p>
      } @else {
        <div class="overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
          <table [class]="ui.table">
            <caption class="sr-only">
              Compras, de la más reciente a la más antigua
            </caption>
            <thead>
              <tr>
                <th scope="col">Fecha</th>
                <th scope="col">Email</th>
                <th scope="col">Producto</th>
                <th scope="col">Monto</th>
                <th scope="col">Estado</th>
              </tr>
            </thead>
            <tbody>
              @for (order of orders.value(); track order.id) {
                <tr>
                  <td class="whitespace-nowrap">{{ order.paidAt | dateTime }}</td>
                  <td>{{ order.email }}</td>
                  <td>
                    {{ order.productName }}
                    <span class="block text-xs text-slate-600">{{
                      order.productType === 'plantilla' ? 'Plantilla' : 'Paquete'
                    }}</span>
                  </td>
                  <td>{{ order.pricePaidCents | usd }}</td>
                  <td>
                    <span [class]="order.status === 'Paid' ? ui.published : ui.draft">
                      {{ order.status === 'Paid' ? 'Pagada' : 'Reembolsada' }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    } @else {
      <p class="text-slate-600">Cargando…</p>
    }
  `,
})
export class OrderList {
  protected readonly ui = ui;
  protected readonly orders = httpResource<OrderListItem[]>(() => `${ADMIN_API}/compras`);
  protected readonly message = problemMessage;
}
