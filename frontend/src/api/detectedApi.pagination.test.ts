import { describe, expect, it } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';
import { api } from '@/api/baseApi';
import { detectedApi } from '@/api/detectedApi';
import { getDb, listDetectedDevices, resetDb, setAuthUser } from '@/mocks/db';
import type { DetectedDevice } from '@/lib/domain';
import '@/store';

function makeStore() {
  return configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (gdm) => gdm().concat(api.middleware),
  });
}

/** Insert brand-new detected devices with the newest lastSeenAt (simulates live arrivals mid-scroll). */
function insertNewDevices(db: ReturnType<typeof getDb>, count: number): void {
  const now = Date.now();
  for (let i = 0; i < count; i++) {
    const mac = `00:1A:2B:AA:0${Math.floor(i / 10)}:${String(i % 100).padStart(2, '0')}`;
    db.detectedDevices.push({
      id: `inserted-${i}`,
      mac,
      macNormalized: mac.replace(/:/g, '').toLowerCase(),
      vendorOui: '00:1A:2B',
      vendorName: 'Intel Corporate',
      deviceType: 'AP',
      ssidLatest: `InsertedNet-${i}`,
      btNameLatest: null,
      channelLatest: 6,
      firstSeenAt: new Date(now + i * 1000).toISOString(),
      lastSeenAt: new Date(now + i * 1000).toISOString(),
      latitude: 48.1,
      longitude: 11.5,
      detectionCount: 1,
    });
  }
}

describe('detected-devices keyset pagination under inserts', () => {
  it('walks the full list with no duplicates or skips when nothing changes', () => {
    const db = resetDb();
    setAuthUser(db.users[0]);
    const expected = db.detectedDevices.length;
    const seen = new Set<string>();
    let cursor: string | null = null;
    let collected = 0;
    let iterations = 0;

    while (iterations++ < 1000) {
      const page = listDetectedDevices({ cursor, limit: 50 });
      for (const d of page.items) {
        expect(seen.has(d.macNormalized)).toBe(false);
        seen.add(d.macNormalized);
      }
      collected += page.items.length;
      if (!page.nextCursor) break;
      cursor = page.nextCursor;
    }

    expect(collected).toBe(expected);
    expect(seen.size).toBe(expected);
  });

  it('walks the full list in strict (lastSeenAt, id) descending order', () => {
    resetDb();
    const rows: DetectedDevice[] = [];
    let cursor: string | null = null;
    while (true) {
      const page = listDetectedDevices({ cursor, limit: 37 });
      rows.push(...page.items);
      if (!page.nextCursor) break;
      cursor = page.nextCursor;
    }
    for (let i = 1; i < rows.length; i++) {
      const prev = rows[i - 1];
      const curr = rows[i];
      const ordered =
        prev.lastSeenAt > curr.lastSeenAt || (prev.lastSeenAt === curr.lastSeenAt && prev.id > curr.id);
      expect(ordered).toBe(true);
    }
  });

  it('inserting new devices mid-scroll does not duplicate or skip pre-existing rows', () => {
    const db = resetDb();
    const before = new Set(db.detectedDevices.map((d) => d.macNormalized));

    // Take the first page, then let new devices arrive.
    const first = listDetectedDevices({ limit: 25 });
    insertNewDevices(db, 12);

    // Continue scrolling from the first page's cursor.
    const seen = new Set(first.items.map((d) => d.macNormalized));
    let cursor = first.nextCursor;
    let guard = 0;
    while (cursor && guard++ < 1000) {
      const page = listDetectedDevices({ cursor, limit: 25 });
      for (const d of page.items) {
        expect(seen.has(d.macNormalized)).toBe(false);
        seen.add(d.macNormalized);
      }
      cursor = page.nextCursor;
    }

    // Every row that existed before the insert is returned exactly once.
    for (const mac of before) {
      expect(seen.has(mac)).toBe(true);
    }
    expect(seen.size).toBeGreaterThanOrEqual(before.size);
  });

  it('RTK Query merge accumulates pages without duplicate rows', async () => {
    const db = resetDb();
    setAuthUser(db.users[0]);
    const store = makeStore();
    const list = detectedApi.endpoints.listDetectedDevices;
    const select = list.select({ limit: 20 });

    await store.dispatch(list.initiate({ limit: 20 })).unwrap();
    const first = select(store.getState()).data!;
    expect(first.items).toHaveLength(20);
    expect(first.nextCursor).not.toBeNull();

    // New arrivals mid-scroll, then fetch the next page.
    insertNewDevices(db, 8);
    await store.dispatch(list.initiate({ limit: 20, cursor: first.nextCursor })).unwrap();

    const merged = select(store.getState()).data!;
    const ids = merged.items.map((d) => d.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(merged.items.length).toBeGreaterThanOrEqual(40);
  });
});
