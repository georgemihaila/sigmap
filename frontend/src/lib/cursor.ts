/**
 * Keyset (cursor) pagination helpers.
 *
 * Mirrors the backend contract: a cursor encodes the tuple `(sort_key, id)`
 * that the page starts *after* (strictly before, given DESC ordering).
 * `WHERE (sort_key, id) < (cursor_key, cursor_id) ORDER BY sort_key DESC, id DESC`.
 */
export interface Cursor {
  sortKey: string;
  id: string;
}

export function encodeCursor(sortKey: string, id: string): string {
  return btoa(JSON.stringify({ sortKey, id } satisfies Cursor));
}

export function decodeCursor(cursor: string | undefined | null): Cursor | null {
  if (!cursor) return null;
  try {
    const parsed = JSON.parse(atob(cursor)) as Partial<Cursor>;
    if (typeof parsed.sortKey !== 'string' || typeof parsed.id !== 'string') return null;
    return { sortKey: parsed.sortKey, id: parsed.id };
  } catch {
    return null;
  }
}

/**
 * True when `a` sorts strictly before `b` under `(key desc, id desc)`.
 * ISO-8601 timestamps compare correctly as strings.
 */
export function keyIsBefore(a: Cursor, b: Cursor): boolean {
  if (a.sortKey !== b.sortKey) return a.sortKey < b.sortKey;
  return a.id < b.id;
}

/** Slice a pre-sorted (desc) array from the given cursor, returning a page. */
export function applyKeyset<T extends { id: string }>(
  rows: T[],
  keyOf: (row: T) => string,
  cursor: Cursor | null,
  limit: number,
): T[] {
  let start = 0;
  if (cursor) {
    start = rows.findIndex((row) => keyIsBefore({ sortKey: keyOf(row), id: row.id }, cursor));
    if (start === -1) return [];
  }
  return rows.slice(start, start + limit);
}
