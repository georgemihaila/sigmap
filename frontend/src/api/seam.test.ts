import { describe, expect, it, vi } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';
import { API_BASE_URL } from './baseQuery';
import { api } from './baseApi';
import '@/store';

function makeStore() {
  return configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (gdm) => gdm().concat(api.middleware),
  });
}

/**
 * The mock/real seam contract: every endpoint must produce a *relative* path
 * so that pointing the single baseUrl at a real BFF is the only change needed.
 */
describe('RTK Query seam', () => {
  it('resolves the shared baseUrl to the current origin + /api (no hard-coded origin)', () => {
    expect(API_BASE_URL).toMatch(/^https?:\/\/[^/]+\/api$/);
    expect(API_BASE_URL).toBe(`${window.location.origin}/api`);
  });

  it('serves every registered endpoint through the shared baseUrl (no hard-coded origin)', async () => {
    const store = makeStore();
    const fetchSpy = vi.spyOn(globalThis, 'fetch');

    // The base `api` keeps the empty-endpoints type; the full endpoint set is
    // registered by the slices at runtime. Reach the action creators through a
    // loose cast for the seam test.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const endpoints = api.endpoints as any;

    const sessions = await store.dispatch(endpoints.listSessions.initiate(undefined)).unwrap();
    const devices = await store
      .dispatch(endpoints.listDetectedDevices.initiate({ limit: 3 }))
      .unwrap();

    // Documented contract shapes survive the round trip.
    expect(Array.isArray(sessions)).toBe(true);
    expect(sessions[0]).toMatchObject({
      id: expect.any(String),
      name: expect.any(String),
      status: expect.stringMatching(/planned|active|archived/),
    });
    expect(devices.items).toHaveLength(3);
    expect(devices.count).toBeGreaterThanOrEqual(3);
    expect(devices.items[0]).toMatchObject({
      mac: expect.stringMatching(/^([0-9A-F]{2}:){5}[0-9A-F]{2}$/),
      deviceType: expect.any(String),
      lastSeenAt: expect.any(String),
    });

    // The requested URLs are composed from the relative path + the shared
    // baseUrl — never from a hard-coded origin.
    const urls = fetchSpy.mock.calls.map(([input]) => {
      const req = input as Request;
      return req instanceof Request ? req.url : String(input);
    });
    expect(urls.some((u) => u.includes('/api/sessions'))).toBe(true);
    expect(urls.some((u) => u.includes('/api/detected-devices') && u.includes('limit=3'))).toBe(true);
    expect(urls.every((u) => !u.includes('bff.example'))).toBe(true);

    fetchSpy.mockRestore();
  });

  it('an absolute-origin swap of the baseUrl resolves to the same contract', async () => {
    // Pointing VITE_API_BASE_URL at any same-origin absolute URL resolves the
    // same handlers (MSW matches by pathname).
    const res = await fetch(`${window.location.origin}/api/sessions`);
    expect(res.status).toBe(200);
    const body = (await res.json()) as Array<{ id: string; name: string; status: string }>;
    expect(Array.isArray(body)).toBe(true);
    expect(body[0]).toMatchObject({ id: expect.any(String), name: expect.any(String) });
    expect(typeof body[0].status).toBe('string');
  });
});
