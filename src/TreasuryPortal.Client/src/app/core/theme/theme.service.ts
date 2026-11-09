import { Injectable, signal } from '@angular/core';

export type AppTheme = 'light' | 'dark';

const STORAGE_KEY = 'tp-theme';

/**
 * Светлая или тёмная схема. Значение хранится в localStorage и применяется атрибутом
 * data-theme на <html> — тот же атрибут ставит ранний скрипт в index.html, чтобы при
 * загрузке не было вспышки белого.
 *
 * Ни одного цвета здесь нет: всё решает color-scheme и токены темы (styles/theme.scss).
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly current = signal<AppTheme>(readInitial());

  readonly theme = this.current.asReadonly();

  set(theme: AppTheme): void {
    this.current.set(theme);
    document.documentElement.dataset['theme'] = theme;
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // Приватный режим или заблокированные данные сайта — тема просто не запомнится
    }
  }

  toggle(): void {
    this.set(this.current() === 'dark' ? 'light' : 'dark');
  }
}

function readInitial(): AppTheme {
  const applied = document.documentElement.dataset['theme'];
  if (applied === 'dark' || applied === 'light') {
    return applied;
  }

  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === 'dark' || saved === 'light') {
      return saved;
    }
  } catch {
    // см. выше
  }

  return matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}
