import '@testing-library/jest-dom';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from '@/mocks/server';
import { getDb, resetDb, setAuthUser } from '@/mocks/db';

// jsdom doesn't implement these browser APIs used by next-themes / Radix.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
globalThis.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
  // Tests run as the operator by default so data-fetching works.
  setAuthUser(getDb().users[0]);

  // Node's undici fetch rejects relative URLs; browsers resolve them against
  // the page origin. Wrap the (already MSW-patched) fetch so the relative
  // requests produced by fetchBaseQuery(baseUrl: "/api") reach MSW.
  const mswFetch = globalThis.fetch;
  globalThis.fetch = ((input: RequestInfo | URL, init?: RequestInit) => {
    const url =
      typeof input === 'string' && input.startsWith('/')
        ? new URL(input, window.location.href).toString()
        : input;
    return mswFetch(url as RequestInfo | URL, init);
  }) as typeof fetch;
});

afterEach(() => {
  server.resetHandlers();
  resetDb();
  setAuthUser(getDb().users[0]);
});

afterAll(() => server.close());
