import { Component } from '@angular/core';

@Component({
  selector: 'app-faq',
  template: `
    <section aria-labelledby="faq-titulo" class="mx-auto max-w-3xl px-4 py-14">
      <h2 id="faq-titulo" class="text-2xl font-bold text-ink">Preguntas frecuentes</h2>
      <div class="mt-6 divide-y divide-line rounded-xl border border-line bg-surface">
        @for (item of items; track item.question) {
          <details class="p-4">
            <summary class="cursor-pointer font-semibold text-ink">{{ item.question }}</summary>
            <p class="mt-2 text-ink-muted">{{ item.answer }}</p>
          </details>
        }
      </div>
    </section>
  `,
})
export class Faq {
  protected readonly items = [
    {
      question: '¿Cómo recibo el archivo?',
      answer:
        'Al terminar el pago te enviamos un email con tu enlace de compra. Desde ahí descargas el archivo de Excel y abres tu copia en Google Sheets. No necesitas crear una cuenta.',
    },
    {
      question: '¿Funciona en Mac y con Google Sheets?',
      answer:
        'Sí. Cada plantilla incluye un archivo .xlsx que abre Excel en Windows y Mac, y una versión de Google Sheets que copias a tu propio Drive con un clic.',
    },
    {
      question: '¿Puedo pedir un reembolso?',
      answer:
        'Sí. Si la plantilla no te sirve, te devolvemos tu dinero dentro de los 14 días posteriores a la compra, sin trámites complicados.',
    },
  ];
}
