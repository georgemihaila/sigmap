import { useEffect, useState } from 'react';

export type Page = 'live' | 'sessions' | 'fleet' | 'presets' | 'devices' | 'exports' | 'pairing';

const VALID_PAGES: readonly Page[] = ['live', 'sessions', 'fleet', 'presets', 'devices', 'exports', 'pairing'];

function pageFromPath(path: string): Page {
  const segment = path.replace(/^\/+/, '').split('/')[0] ?? '';
  return (VALID_PAGES as readonly string[]).includes(segment) ? (segment as Page) : 'live';
}

function pathForPage(page: Page): string {
  return page === 'live' ? '/' : `/${page}`;
}

/**
 * Path-based navigation that survives reloads. Uses the History API; the Vite
 * dev server and the prod nginx config both fall back to index.html for deep
 * paths, so F5 keeps you on the same page.
 */
export function usePage(): [Page, (page: Page) => void] {
  const [page, setPage] = useState<Page>(() => pageFromPath(window.location.pathname));

  useEffect(() => {
    const onPopState = () => setPage(pageFromPath(window.location.pathname));
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, []);

  const navigate = (next: Page) => {
    setPage(next);
    const url = pathForPage(next);
    if (window.location.pathname !== url) {
      window.history.pushState({}, '', url);
    }
  };

  return [page, navigate];
}
