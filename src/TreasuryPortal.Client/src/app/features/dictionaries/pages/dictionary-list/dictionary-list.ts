import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';

import { EmptyState } from '@shared/components/empty-state/empty-state';
import { PageHeader } from '@shared/components/page-header/page-header';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { ListController, ListStateService } from '@shared/utils/list-controller';
import { DictionariesApi } from '../../data/dictionaries.api';
import { DictionaryItem } from '../../data/dictionary.model';

/**
 * Образец экрана-списка: выбор справочника, поиск, сортировка и постраничный вывод.
 *
 * Делая свой список, копируйте этот файл: загрузка через rxResource (значит, через HttpClient,
 * значит, через NTLM-гейт и обработку ошибок), поведение таблицы — в ListController, состояние
 * поиска и страницы переживает уход в карточку и обратно.
 */
@Component({
  selector: 'tp-dictionary-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatIconModule,
    PageHeader,
    EmptyState,
    AppDatePipe,
  ],
  templateUrl: './dictionary-list.html',
  styleUrl: './dictionary-list.scss',
})
export class DictionaryList {
  private readonly api = inject(DictionariesApi);

  protected readonly columns = ['code', 'name', 'dateFrom', 'dateTo'] as const;

  /**
   * Справочники, которые показывает экран. Список временный: как только появится процедура
   * «перечислить справочники», он придёт с сервера.
   */
  protected readonly available = [
    { value: 'currency', label: 'Валюти' },
    { value: 'operationType', label: 'Типи операцій' },
    { value: 'status', label: 'Статуси' },
  ];

  protected readonly selected = signal(this.available[0].value);

  private readonly items = rxResource({
    params: () => this.selected(),
    stream: ({ params }) => this.api.getOne(params),
    defaultValue: [] as DictionaryItem[],
  });

  protected readonly isLoading = this.items.isLoading;

  /** Сообщение об ошибке уже показал интерсептор; здесь — чтобы не писать «немає записів». */
  protected readonly hasError = computed(() => this.items.error() !== undefined);

  private readonly paginator = viewChild(MatPaginator);
  private readonly sort = viewChild(MatSort);

  protected readonly list = new ListController<DictionaryItem>(
    {
      state: inject(ListStateService).get('dictionaries'),
      key: row => String(row.elementID),
      searchFields: ['code', 'name', 'shortName'],
    },
    this.paginator,
    this.sort,
  );

  constructor() {
    // hasValue() обязателен: у ресурса в состоянии ошибки value() бросает исключение
    effect(() => this.list.rows.set(this.items.hasValue() ? this.items.value() : []));
  }
}
