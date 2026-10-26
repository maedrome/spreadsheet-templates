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
        'Al confirmarse el pago te enviamos un email con tu Enlace de compra. Desde ahí descargas el archivo .xlsx y abres tu copia en Google Sheets. La entrega es inmediata y no necesitas crear una cuenta.',
    },
    {
      question: '¿Funciona en Mac y con Google Sheets?',
      answer:
        'Sí. Cada Plantilla incluye un archivo .xlsx que abre Excel en Windows y Mac, y una copia de Google Sheets que pasa a tu propia cuenta de Google con un clic.',
    },
    {
      question: '¿Puedo pedir un reembolso?',
      answer:
        'Sí. Tienes 14 días desde tu Compra para pedir el reembolso total, sin preguntas: solo escribe al email de contacto. Al reembolsarla, el Enlace de compra deja de dar acceso a las descargas.',
    },
    {
      question: '¿Qué pasa si pierdo el email?',
      answer:
        'Entra a "Recuperar mi compra", escribe el email con el que pagaste y te enviamos de nuevo los enlaces de tus Compras.',
    },
  ];
}
