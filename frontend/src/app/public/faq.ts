import { Component } from '@angular/core';

@Component({
  selector: 'app-faq',
  template: `
    <section
      aria-labelledby="faq-titulo"
      class="mx-auto grid max-w-6xl items-start gap-6 px-4 py-14 sm:py-20 lg:grid-cols-[1fr_2fr] lg:gap-12"
    >
      <h2 id="faq-titulo" class="text-3xl font-bold text-ink">Preguntas frecuentes</h2>
      <div class="divide-y divide-line rounded-cell border border-line bg-surface">
        @for (item of items; track item.question) {
          <details class="group">
            <summary
              class="flex cursor-pointer list-none items-center justify-between gap-4 p-4 font-semibold text-ink hover:text-accent-ink [&::-webkit-details-marker]:hidden"
            >
              {{ item.question }}
              <span
                aria-hidden="true"
                class="grid size-6 shrink-0 place-items-center rounded-chip border border-line font-mono text-sm text-ink-subtle group-open:border-accent group-open:bg-accent group-open:text-on-accent"
              >
                <span class="block transition-transform group-open:rotate-45">+</span>
              </span>
            </summary>
            <p class="px-4 pb-4 text-ink-muted">{{ item.answer }}</p>
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
