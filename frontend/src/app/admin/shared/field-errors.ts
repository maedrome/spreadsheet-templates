import { Component, Signal, computed, input } from '@angular/core';
import { ui } from './ui';

interface FieldStateLike {
  touched: Signal<boolean>;
  invalid: Signal<boolean>;
  errors: Signal<readonly { message?: string }[]>;
}

/** Los errores de un campo de Signal Forms una vez que el usuario lo tocó o intentó enviar. */
@Component({
  selector: 'app-field-errors',
  host: { 'aria-live': 'polite', class: 'block' },
  template: `
    @if (visible()) {
      @for (error of state().errors(); track $index) {
        <p [class]="ui.error">{{ error.message }}</p>
      }
    }
  `,
})
export class FieldErrors {
  protected readonly ui = ui;
  readonly field = input.required<() => FieldStateLike>();

  protected readonly state = computed(() => this.field()());
  protected readonly visible = computed(() => this.state().touched() && this.state().invalid());
}
