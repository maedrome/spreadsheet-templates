import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageMeta } from '../page-meta';
import { LEGAL_INFO } from './legal-info';
import { LegalPage } from './legal-page';

@Component({
  selector: 'app-privacy',
  imports: [LegalPage, RouterLink],
  template: `
    <app-legal-page heading="Política de privacidad">
      <p>
        Esta política explica cómo tratamos tus datos personales, de acuerdo con la Ley Orgánica de
        Protección de Datos Personales (LOPDP) de Ecuador. Resumen: usamos tu email solo para
        entregarte tu Compra y para que puedas recuperarla. No enviamos marketing ni newsletter.
      </p>

      <h2>1. Responsable del tratamiento</h2>
      <ul>
        <li>Titular: {{ info.ownerName }} (persona natural)</li>
        <li>RUC: {{ info.ruc }}</li>
        <li>Tienda: {{ info.storeName }} ({{ info.domain }})</li>
        <li>Ubicación: {{ info.city }}, {{ info.country }}</li>
        <li>
          Contacto:
          <a [href]="'mailto:' + info.contactEmail">{{ info.contactEmail }}</a>
        </li>
      </ul>

      <h2>2. Datos que recogemos</h2>
      <ul>
        <li>Tu <strong>email</strong>, que escribes al pagar.</li>
        <li>
          Los <strong>datos de la Compra</strong> que nos envía Polar.sh: qué Plantilla o Paquete
          compraste, el monto, la fecha y su estado (por ejemplo, Reembolsada).
        </li>
      </ul>
      <p>
        La tienda <strong>no recibe ni guarda datos de tarjeta</strong>: el pago se hace en el
        checkout de Polar.sh.
      </p>

      <h2>3. Para qué los usamos</h2>
      <p>
        Solo para dos cosas: entregarte tu Compra mediante un Enlace de compra por email y permitir
        que la recuperes en <a routerLink="/recuperar">Recuperar mi compra</a>. No los usamos para
        marketing, no enviamos newsletter y no los vendemos ni cedemos para otros fines. La base
        legal es la ejecución del contrato de compra que celebras con nosotros.
      </p>

      <h2>4. Con quién los compartimos (encargados)</h2>
      <ul>
        <li><strong>Polar.sh:</strong> procesa los pagos como Merchant of Record.</li>
        <li><strong>Resend:</strong> envía los emails transaccionales (el Enlace de compra).</li>
        <li><strong>Hetzner:</strong> aloja el servidor donde se guarda la información.</li>
        <li>
          <strong>Umami</strong> (autohospedado en nuestro servidor): analítica de visitas sin
          cookies.
        </li>
      </ul>
      <p>
        Algunos de estos proveedores están fuera de Ecuador, por lo que tus datos pueden ser
        transferidos internacionalmente. Solo trabajamos con proveedores que aplican medidas de
        protección adecuadas.
      </p>

      <h2>5. Cuánto tiempo los conservamos</h2>
      <p>
        Conservamos tu email y los datos de la Compra mientras sean necesarios para entregarla y
        permitirte recuperarla, y durante el tiempo que exijan las obligaciones legales y
        tributarias. Después los eliminamos o anonimizamos. Puedes pedir su eliminación antes (ver
        punto 7).
      </p>

      <h2>6. Seguridad</h2>
      <p>
        Aplicamos medidas técnicas y organizativas razonables: conexión cifrada (HTTPS), acceso
        restringido al panel de administración y los Enlaces de compra se guardan cifrados con hash,
        de modo que no se almacenan en texto legible.
      </p>

      <h2>7. Tus derechos</h2>
      <p>
        Como titular de los datos tienes derecho de <strong>acceso</strong>,
        <strong>rectificación</strong>, <strong>eliminación</strong>, <strong>oposición</strong>,
        <strong>portabilidad</strong> y <strong>suspensión del tratamiento</strong>. Para ejercerlos,
        escribe a <a [href]="'mailto:' + info.contactEmail">{{ info.contactEmail }}</a> desde el
        email con el que compraste. Respondemos dentro de los plazos que fija la ley.
      </p>
      <p>
        Si consideras que no atendimos tu solicitud, puedes presentar un reclamo ante la autoridad
        de protección de datos personales de Ecuador (Superintendencia de Protección de Datos
        Personales).
      </p>

      <h2>8. Cookies</h2>
      <p>
        El sitio público no usa cookies de rastreo ni de publicidad. Solo se usan cookies técnicas,
        necesarias para el funcionamiento del panel de administración, al que los clientes no
        acceden.
      </p>

      <h2>9. Cambios a esta política</h2>
      <p>
        Si la actualizamos, publicaremos la versión nueva aquí con su fecha. Las condiciones de
        compra están en los <a routerLink="/terminos">términos y condiciones</a>.
      </p>
    </app-legal-page>
  `,
})
export class PrivacyPage {
  protected readonly info = LEGAL_INFO;

  constructor() {
    inject(PageMeta).set({
      title: 'Política de privacidad',
      description:
        'Cómo tratamos tu email y los datos de tu compra, conforme a la LOPDP de Ecuador. Sin marketing ni newsletter.',
    });
  }
}
