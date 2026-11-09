import { Directive, ElementRef, HostListener, computed, inject, input } from '@angular/core';

/**
 * Поле, в которое можно ввести только число. Перенесена из BanknoteOps; селектор `digtsOnly`
 * оставлен рядом с новым `tpDigitsOnly`, чтобы разметка, скопированная из того проекта,
 * работала без правок.
 *
 * Фильтрует ввод с клавиатуры, вставку и drop, а не чистит значение задним числом, — поэтому
 * в модель никогда не попадает мусор и не дёргается валидация формы.
 *
 * Примеры:
 *   Только целые:                      <input tpDigitsOnly />
 *   Дробные без ограничения знаков:    <input tpDigitsOnly [decimal]="true" />
 *   Дробные, 3 знака после комы:       <input tpDigitsOnly [decimal]="true" [decimalPrecision]="3" />
 *   С границами:                       <input tpDigitsOnly [decimal]="true" [decimalPrecision]="2" [min]="0" [max]="9999" />
 *   С шаблоном:                        <input tpDigitsOnly [decimal]="true" pattern="^\d{1,7}(\.\d{1,3})?$" />
 */
@Directive({
  selector: '[tpDigitsOnly], [digtsOnly]',
})
export class DigitsOnlyDirective {
  readonly decimalPrecision = input<number>(0);
  readonly min = input<number | null>(null);
  readonly max = input<number | null>(null);
  readonly pattern = input<string | null>(null);
  readonly allowNegative = input<boolean>(true);
  readonly allowPaste = input<boolean>(true);

  /** Дробное поле, если передано явно или если задана точность. */
  readonly decimal = input<boolean>(false);
  readonly isDecimal = computed(() => this.decimal() || this.decimalPrecision() > 0);

  readonly regExp = computed(() => {
    const source = this.pattern();
    return source ? new RegExp(source) : null;
  });

  private readonly el = inject<ElementRef<HTMLInputElement>>(ElementRef);

  private readonly navigationKeys: readonly string[] = [
    'Backspace',
    'Delete',
    'Tab',
    'Escape',
    'Enter',
    'NumLock',
    'CapsLock',
    'ScrollLock',
    'Pause',
    'PrintScreen',
    'Home',
    'End',
    'PageUp',
    'PageDown',
    'ArrowLeft',
    'ArrowRight',
    'ArrowUp',
    'ArrowDown',
    'Insert',
    'F1',
    'F2',
    'F3',
    'F4',
    'F5',
    'F6',
    'F7',
    'F8',
    'F9',
    'F10',
    'F11',
    'F12',
    'ContextMenu',
    'Meta',
    'Alt',
    'Control',
    'Shift',
  ];

  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    if (this.navigationKeys.includes(event.key)) {
      return;
    }
    if (
      (event.ctrlKey || event.metaKey) &&
      ['a', 'c', 'v', 'x', 'z', 'y'].includes(event.key.toLowerCase())
    ) {
      return;
    }

    const input = this.el.nativeElement;

    if (event.key === '-' || event.code === 'Minus' || event.code === 'NumpadSubtract') {
      // Минус разрешён только в самом начале и только один
      if (this.allowNegative() && input.selectionStart === 0 && !input.value.includes('-')) {
        return;
      }
      event.preventDefault();
      return;
    }

    if (this.isSeparatorKey(event)) {
      event.preventDefault();
      if (this.isDecimal()) {
        this.insertDot(input);
      }
      return;
    }

    const isDigit = /^\d$/.test(event.key) || /^Numpad\d$/.test(event.code);
    if (!isDigit) {
      event.preventDefault();
      return;
    }

    const char = /^\d$/.test(event.key) ? event.key : event.code.replace('Numpad', '');
    const simulated = this.simulateInsert(input, char);
    if (simulated === null || !this.checkMax(simulated)) {
      event.preventDefault();
      return;
    }

    const re = this.regExp();
    if (re && !re.test(simulated)) {
      event.preventDefault();
    }
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    if (!this.allowPaste()) {
      return;
    }
    const sanitized = this.sanitize(event.clipboardData?.getData('text/plain') ?? '');
    if (sanitized !== '') {
      this.applyToSelection(sanitized);
    }
  }

  @HostListener('drop', ['$event'])
  onDrop(event: DragEvent): void {
    event.preventDefault();
    const sanitized = this.sanitize(event.dataTransfer?.getData('text') ?? '');
    if (sanitized === '') {
      return;
    }

    this.el.nativeElement.focus();
    this.applyToSelection(sanitized);
  }

  @HostListener('blur')
  onBlur(): void {
    const input = this.el.nativeElement;
    const value = parseFloat(input.value);
    const min = this.min();

    if (min !== null && !isNaN(value) && value < min) {
      this.setNativeValue(input, String(min));
    }
  }

  private sanitize(raw: string): string {
    let text = raw.replace(/,/g, '.');
    const negative = this.allowNegative() && text.trimStart().startsWith('-');
    text = text.replace(/[^\d.]/g, '');

    if (!this.isDecimal()) {
      text = text.replace(/\./g, '');
    } else {
      const firstDot = text.indexOf('.');
      if (firstDot !== -1) {
        text = text.slice(0, firstDot + 1) + text.slice(firstDot + 1).replace(/\./g, '');
      }
    }

    const precision = this.decimalPrecision();
    if (this.isDecimal() && precision > 0) {
      const dot = text.indexOf('.');
      if (dot !== -1) {
        text = text.slice(0, dot + 1 + precision);
      }
    }

    let result = (negative ? '-' : '') + text;

    const max = this.max();
    if (max !== null) {
      const value = parseFloat(result);
      if (!isNaN(value) && value > max) {
        result = String(max);
      }
    }

    return result;
  }

  private isSeparatorKey(event: KeyboardEvent): boolean {
    return (
      event.key === '.' ||
      event.key === ',' ||
      event.code === 'Period' ||
      event.code === 'Comma' ||
      event.code === 'NumpadDecimal'
    );
  }

  /** Каким станет значение, если вставить символ, — или null, если такого быть не должно. */
  private simulateInsert(input: HTMLInputElement, char: string): string | null {
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? input.value.length;
    const next = input.value.slice(0, start) + char + input.value.slice(end);

    const precision = this.decimalPrecision();
    if (this.isDecimal() && precision > 0) {
      const dot = next.indexOf('.');
      if (dot !== -1 && next.length - dot - 1 > precision) {
        return null;
      }
    }

    return next;
  }

  private checkMax(value: string): boolean {
    const max = this.max();
    if (max === null) {
      return true;
    }
    const parsed = parseFloat(value);
    return isNaN(parsed) || parsed <= max;
  }

  private insertDot(input: HTMLInputElement): void {
    const value = input.value;
    const start = input.selectionStart ?? value.length;
    const end = input.selectionEnd ?? value.length;
    const existingDot = value.indexOf('.');

    // Точка уже есть и ничего не выделено — просто ставим курсор за неё
    if (existingDot !== -1 && start === end) {
      const position = existingDot + 1;
      requestAnimationFrame(() => input.setSelectionRange(position, value.length));
      return;
    }

    const cleaned = this.sanitize(value.slice(0, start) + '.' + value.slice(end));
    this.setNativeValue(input, cleaned);

    const dot = cleaned.indexOf('.');
    const cursor = dot !== -1 ? dot + 1 : start + 1;
    requestAnimationFrame(() => input.setSelectionRange(cursor, cleaned.length));
  }

  private applyToSelection(text: string): void {
    const input = this.el.nativeElement;
    const start = input.selectionStart ?? 0;
    const end = input.selectionEnd ?? 0;

    const combined = input.value.slice(0, start) + text + input.value.slice(end);
    const result = this.sanitize(combined);
    if (result === '') {
      return;
    }

    const re = this.regExp();
    if (re && !re.test(result)) {
      return;
    }

    this.setNativeValue(input, result);

    const cursor = Math.min(start + text.length, result.length);
    requestAnimationFrame(() => input.setSelectionRange(cursor, cursor));
  }

  /**
   * Значение ставится родным сеттером и сопровождается событием input: иначе Angular не увидит
   * правку и модель разойдётся с тем, что на экране.
   */
  private setNativeValue(input: HTMLInputElement, value: string): void {
    const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;

    if (setter) {
      setter.call(input, value);
    } else {
      input.value = value;
    }

    input.dispatchEvent(new Event('input', { bubbles: true }));
  }
}
