import { SelectionModel } from '@angular/cdk/collections';
import { Injectable, Signal, WritableSignal, computed, effect, signal } from '@angular/core';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSort, Sort, SortDirection } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';

import { buildSearchString, matchesSearch } from './search';

/**
 * Что список помнит, пока пользователь уходит в карточку и возвращается: текст поиска,
 * сортировку, размер страницы и отмеченные строки.
 */
export interface ListState {
  searchText: WritableSignal<string>;
  sortActive: string;
  sortDirection: SortDirection;
  pageSize: number;
  selected: SelectionModel<string>;
}

/**
 * Хранит состояние списков по имени. Казначей за день десятки раз ходит «список → карточка →
 * список», и каждый раз заново набирать фильтр он не должен.
 */
@Injectable({ providedIn: 'root' })
export class ListStateService {
  private readonly states = new Map<string, ListState>();

  get(name: string, pageSize = 25): ListState {
    let state = this.states.get(name);
    if (!state) {
      state = {
        searchText: signal(''),
        sortActive: '',
        sortDirection: '',
        pageSize,
        selected: new SelectionModel<string>(true),
      };
      this.states.set(name, state);
    }
    return state;
  }

  /** Сбрасывает запомненное — например, после выхода из раздела. */
  reset(name: string): void {
    this.states.delete(name);
  }
}

export interface ListOptions<T> {
  state: ListState;
  /** Идентификатор строки для отметок; нужен, только если строки можно отмечать. */
  key?: (row: T) => string;
  /** Дополнительный фильтр рядом с поиском, например «только действующие». */
  filter?: () => (row: T) => boolean;
  /** Поля, по которым ищет строка поиска; даты не ищутся. */
  searchFields: readonly string[];
}

/**
 * Поведение, общее для всех экранов-списков: фильтрация строкой поиска, сортировка, постраничный
 * вывод и отметка строк. Создаётся в инициализаторе поля компонента, поэтому его эффекты живут
 * ровно столько, сколько компонент.
 *
 * Применение в компоненте:
 *   private readonly paginator = viewChild(MatPaginator);
 *   private readonly sort = viewChild(MatSort);
 *   protected readonly list = new ListController<Row>(
 *     { state: inject(ListStateService).get('limits'), searchFields: ['code', 'name'] },
 *     this.paginator, this.sort);
 */
export class ListController<T extends object> {
  readonly rows = signal<T[]>([]);
  readonly searchText: WritableSignal<string>;

  /** Счётчик правок отметок: по нему пересчитываются computed, читающие SelectionModel. */
  private readonly selectionVersion = signal(0);

  readonly filtered = computed(() => {
    const query = this.searchText();
    const extra = this.options.filter?.();
    return this.rows().filter(
      row => matchesSearch(this.searchStringOf(row), query) && (!extra || extra(row)),
    );
  });

  readonly selectedRows = computed(() => {
    this.selectionVersion();
    const key = this.options.key;
    return key ? this.filtered().filter(row => this.state.selected.isSelected(key(row))) : [];
  });

  readonly selectedCount = computed(() => this.selectedRows().length);

  readonly isAllSelected = computed(
    () => this.filtered().length > 0 && this.selectedCount() === this.filtered().length,
  );

  readonly isEmpty = computed(() => this.filtered().length === 0);

  readonly datasource = new MatTableDataSource<T>([]);

  constructor(
    private readonly options: ListOptions<T>,
    paginator: Signal<MatPaginator | undefined>,
    sort: Signal<MatSort | undefined>,
  ) {
    this.searchText = options.state.searchText;
    this.datasource.sortingDataAccessor = sortValue;

    effect(() => {
      this.datasource.data = this.filtered();
    });

    effect(() => {
      this.datasource.paginator = paginator() ?? null;
      this.datasource.sort = sort() ?? null;
    });
  }

  get state(): ListState {
    return this.options.state;
  }

  isSelected(row: T): boolean {
    this.selectionVersion();
    const key = this.options.key;
    return !!key && this.state.selected.isSelected(key(row));
  }

  toggle(row: T, checked: boolean): void {
    const key = this.options.key;
    if (!key) {
      return;
    }
    if (checked) {
      this.state.selected.select(key(row));
    } else {
      this.state.selected.deselect(key(row));
    }
    this.selectionVersion.update(version => version + 1);
  }

  toggleAll(checked: boolean): void {
    const key = this.options.key;
    if (!key) {
      return;
    }
    if (checked) {
      this.state.selected.select(...this.filtered().map(key));
    } else {
      this.state.selected.clear();
    }
    this.selectionVersion.update(version => version + 1);
  }

  clearSelection(): void {
    this.state.selected.clear();
    this.selectionVersion.update(version => version + 1);
  }

  onSortChange(sort: Sort): void {
    this.state.sortActive = sort.active;
    this.state.sortDirection = sort.direction;
  }

  onPage(event: PageEvent): void {
    this.state.pageSize = event.pageSize;
  }

  private searchStringOf(row: T): string {
    return buildSearchString(row, this.options.searchFields);
  }
}

/**
 * Сортировка: даты по времени, текст без учёта регистра и по украинским правилам,
 * вложенные значения адресуются точкой — как и ключи колонок.
 */
function sortValue(row: object, column: string): string | number {
  let value: unknown = row;
  for (const part of column.split('.')) {
    value = (value as Record<string, unknown> | null | undefined)?.[part];
  }

  if (value == null) {
    return '';
  }
  if (value instanceof Date) {
    return value.getTime();
  }
  if (typeof value === 'number') {
    return value;
  }
  if (typeof value === 'boolean') {
    return value ? 1 : 0;
  }
  return String(value).toLocaleLowerCase('uk');
}
