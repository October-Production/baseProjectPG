import { Routes } from '@angular/router';

export const DICTIONARIES_ROUTES: Routes = [
  {
    path: '',
    title: 'Довідники · Treasury Portal',
    loadComponent: () => import('./pages/dictionary-list/dictionary-list').then(m => m.DictionaryList),
  },
];
