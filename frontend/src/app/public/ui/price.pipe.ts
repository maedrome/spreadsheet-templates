import { Pipe, PipeTransform } from '@angular/core';

const usd = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' });

@Pipe({ name: 'price' })
export class PricePipe implements PipeTransform {
  transform(cents: number): string {
    return usd.format(cents / 100);
  }
}
