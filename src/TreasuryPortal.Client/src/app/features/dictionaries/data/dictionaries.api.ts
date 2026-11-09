import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { apiUrl } from '@core/api/api';
import { DictionariesResponse, DictionaryItem, DictionaryName, DictionaryResponse } from './dictionary.model';

/**
 * Обращения к API раздела. Один такой сервис на раздел — компоненты не знают ни адресов,
 * ни формы ответа.
 *
 * Адреса относительные (apiUrl): их резолвит <base href>, поэтому портал работает и в корне
 * сайта, и как виртуальное приложение IIS.
 */
@Injectable({ providedIn: 'root' })
export class DictionariesApi {
  private readonly http = inject(HttpClient);

  getOne(name: DictionaryName | string): Observable<DictionaryItem[]> {
    return this.http
      .get<DictionaryResponse>(apiUrl(`dictionaries/${encodeURIComponent(name)}`))
      .pipe(map(response => response?.items ?? []));
  }

  /** Несколько справочников одним запросом — карточке их нужно сразу несколько. */
  getMany(names: readonly (DictionaryName | string)[]): Observable<Record<string, DictionaryItem[]>> {
    let params = new HttpParams();
    for (const name of names) {
      params = params.append('names', name);
    }

    return this.http
      .get<DictionariesResponse>(apiUrl('dictionaries'), { params })
      .pipe(map(response => response?.dictionaries ?? {}));
  }
}
