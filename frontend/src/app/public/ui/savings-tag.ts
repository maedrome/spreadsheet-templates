import { Component, input } from '@angular/core';
import { PricePipe } from './price.pipe';

/** La etiqueta del Ahorro de un Paquete. */
@Component({
  selector: 'app-savings-tag',
  imports: [PricePipe],
  host: {
    class:
      'block w-fit rounded-chip bg-night px-2 py-1 font-mono text-xs font-medium leading-none text-mint',
  },
  template: `Ahorras {{ cents() | price }}`,
})
export class SavingsTag {
  readonly cents = input.required<number>();
}
