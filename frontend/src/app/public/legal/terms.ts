import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageMeta } from '../page-meta';
import { LEGAL_INFO } from './legal-info';
import { LegalPage } from './legal-page';

@Component({
  selector: 'app-terms',
  imports: [LegalPage, RouterLink],
  template: `
    <app-legal-page heading="Términos y condiciones">
      <p>
        Estos términos regulan la compra y el uso de las Plantillas y los Paquetes que vende
        <strong>{{ info.storeName }}</strong> en <strong>{{ info.domain }}</strong
        >. Al comprar aceptas lo que sigue.
      </p>

      <h2>1. Quién vende</h2>
      <p>
        La tienda la opera {{ info.ownerName }}, persona natural con RUC {{ info.ruc }}, en
        {{ info.city }}, {{ info.country }}. Para el cobro, el vendedor de registro es Polar.sh (ver
        el punto 3).
      </p>

      <h2>2. Qué se vende</h2>
      <ul>
        <li>
          <strong>Plantillas:</strong> hojas de cálculo de presupuesto digitales. Cada una incluye
          un archivo <code>.xlsx</code> y una copia de Google Sheets.
        </li>
        <li>
          <strong>Paquetes:</strong> varias Plantillas elegidas por la tienda, a un precio propio.
          Tu Compra incluye las Plantillas que tenía el Paquete en el momento de pagar, aunque
          después cambie.
        </li>
      </ul>
      <p>
        No hay cuentas de cliente. Cuando el pago se confirma, la entrega es inmediata: recibes por
        email un <strong>Enlace de compra</strong> privado desde el que descargas tus archivos.
        Cualquiera que tenga ese enlace puede descargar, así que no lo compartas.
      </p>

      <h2>3. Pagos</h2>
      <p>
        Los pagos los procesa <strong>Polar.sh</strong> como <strong>Merchant of Record</strong>.
        Esto significa que Polar es el vendedor de registro: cobra tu pago, calcula y cobra los
        impuestos que correspondan según tu país y emite el recibo. Los precios están en dólares
        estadounidenses (USD) y se paga solo con tarjeta. La tienda no recibe ni guarda los datos de
        tu tarjeta.
      </p>

      <h2>4. Licencia de uso</h2>
      <ul>
        <li>
          Puedes usar las Plantillas para tu uso personal, en tu hogar o en tu propio negocio.
        </li>
        <li>
          Está prohibido revender, redistribuir, compartir o publicar las Plantillas, completas o
          modificadas, de forma gratuita o de pago.
        </li>
        <li>
          La copia de Google Sheets queda en tu propia cuenta de Google. A partir de ahí, la
          información que guardes allí es tuya y la gestionas tú.
        </li>
      </ul>

      <h2>5. Actualizaciones</h2>
      <p>
        Podemos mejorar o corregir una Plantilla. La descarga desde tu Enlace de compra siempre
        entrega la versión vigente del archivo. Las copias que ya descargaste o copiaste a Google
        Sheets no se modifican.
      </p>

      <h2>6. Reembolsos</h2>
      <p>
        Tienes <strong>{{ info.refundDays }} días</strong> desde tu Compra para pedir un reembolso
        total, sin preguntas. Escribe a
        <a [href]="'mailto:' + info.contactEmail">{{ info.contactEmail }}</a> indicando el email con
        el que pagaste. Cuando la Compra pasa a Reembolsada, el dinero vuelve a tu tarjeta (el plazo
        exacto depende de tu banco) y el Enlace de compra deja de dar acceso a las descargas.
      </p>

      <h2>7. Si pierdes tu Enlace de compra</h2>
      <p>
        Entra a <a routerLink="/recuperar">Recuperar mi compra</a>, escribe el email con el que
        pagaste y te enviamos de nuevo los enlaces de tus Compras.
      </p>

      <h2>8. Limitación de responsabilidad</h2>
      <p>
        Las Plantillas son herramientas de organización. No son asesoría financiera, contable,
        tributaria ni legal, y las decisiones que tomes con ellas son tuyas. Se entregan tal como
        están; en la medida que permita la ley, nuestra responsabilidad máxima se limita al monto
        que pagaste por la Compra.
      </p>

      <h2>9. Datos personales</h2>
      <p>
        Cómo tratamos tu email y los datos de tu Compra está explicado en la
        <a routerLink="/privacidad">política de privacidad</a>.
      </p>

      <h2>10. Ley aplicable</h2>
      <p>
        Estos términos se rigen por las leyes de {{ info.country }}. Cualquier controversia se
        someterá a los jueces competentes de {{ info.city }}, {{ info.country }}, sin perjuicio de
        los derechos que la ley reconozca a los consumidores.
      </p>

      <h2>11. Cambios a los términos</h2>
      <p>
        Podemos actualizar estos términos. Publicaremos la versión nueva en esta página con su fecha
        de actualización. Los cambios no afectan a las Compras ya realizadas.
      </p>

      <h2>12. Contacto</h2>
      <p>
        Para cualquier consulta, escribe a
        <a [href]="'mailto:' + info.contactEmail">{{ info.contactEmail }}</a
        >.
      </p>
    </app-legal-page>
  `,
})
export class TermsPage {
  protected readonly info = LEGAL_INFO;

  constructor() {
    inject(PageMeta).set({
      title: 'Términos y condiciones',
      description:
        'Condiciones de compra, licencia de uso y reembolso total en 14 días de nuestras plantillas de presupuesto.',
    });
  }
}
