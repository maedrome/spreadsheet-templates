import { httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ADMIN_API, AdminApi, OrderListItem } from '../admin-api';
import { DateTimePipe, UsdPipe, problemMessage } from '../shared/format';
import { ui } from '../shared/ui';

/** Las Compras, con el reenvío del Enlace de compra por email. */
@Component({
  selector: 'app-admin-order-list',
  imports: [UsdPipe, DateTimePipe],
  template: `
    <h1 class="mb-6 text-2xl font-semibold">Compras</h1>

    <p role="status" class="mb-4 min-h-6 text-sm" [class.text-danger]="notice()?.error">
      {{ notice()?.text }}
    </p>

    @if (orders.error(); as error) {
      <p role="alert" class="text-danger">{{ message(error) }}</p>
    } @else if (orders.hasValue()) {
      @if (orders.value().length === 0) {
        <p class="text-ink-subtle">Todavía no hay Compras.</p>
      } @else {
        <div class="overflow-x-auto rounded-cell border border-line bg-surface">
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
                <th scope="col"><span class="sr-only">Acciones</span></th>
              </tr>
            </thead>
            <tbody>
              @for (order of orders.value(); track order.id) {
                <tr>
                  <td class="whitespace-nowrap">{{ order.paidAt | dateTime }}</td>
                  <td>{{ order.email }}</td>
                  <td>
                    {{ order.productName }}
                    <span class="block text-xs text-ink-subtle">{{
                      order.productType === 'plantilla' ? 'Plantilla' : 'Paquete'
                    }}</span>
                  </td>
                  <td>{{ order.pricePaidCents | usd }}</td>
                  <td>
                    <span [class]="order.status === 'Paid' ? ui.published : ui.draft">
                      {{ order.status === 'Paid' ? 'Pagada' : 'Reembolsada' }}
                    </span>
                  </td>
                  <td class="whitespace-nowrap text-right">
                    @if (order.status === 'Paid') {
                      <button
                        type="button"
                        [class]="ui.secondary"
                        [disabled]="sending() === order.id"
                        [attr.aria-label]="'Reenviar el Enlace de compra a ' + order.email"
                        (click)="resend(order)"
                      >
                        {{ sending() === order.id ? 'Enviando…' : 'Reenviar enlace' }}
                      </button>
                    }
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
export class OrderList {
  protected readonly ui = ui;
  protected readonly orders = httpResource<OrderListItem[]>(() => `${ADMIN_API}/compras`);
  protected readonly message = problemMessage;
  protected readonly sending = signal<number | null>(null);
  protected readonly notice = signal<{ text: string; error: boolean } | null>(null);
  private readonly api = inject(AdminApi);

  protected async resend(order: OrderListItem): Promise<void> {
    this.sending.set(order.id);
    this.notice.set(null);
    try {
      await this.api.resendPurchaseLink(order.id);
      this.notice.set({
        text: `Enviamos un Enlace de compra nuevo a ${order.email}.`,
        error: false,
      });
    } catch (error) {
      this.notice.set({ text: problemMessage(error), error: true });
    } finally {
      this.sending.set(null);
    }
  }
}
