/** Режим карточки, передаётся в query-параметре `pageMode`. */
export enum PageMode {
  Read = 1,
  Edit = 2,
  Add = 3,
}

/** Параметр приходит текстом; всё неизвестное открывает карточку только на чтение. */
export function toPageMode(value: string | number | null | undefined): PageMode {
  const mode = Number(value);
  return mode === PageMode.Edit || mode === PageMode.Add ? mode : PageMode.Read;
}

export function isEditable(mode: PageMode): boolean {
  return mode !== PageMode.Read;
}
