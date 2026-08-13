import { fetchBaseQuery } from '@reduxjs/toolkit/query/react';

/**
 * The backend seam. Everything in the app talks to this baseUrl.
 *
 * Default is same-origin `/api`, proxied to the backend by Vite in dev. Set
 * `VITE_API_BASE_URL` to point the whole app at another backend.
 */
export const API_BASE_URL = ((): string => {
  const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
  if (configured) return configured;
  // In the browser a relative "/api" is resolved against the page origin.
  // Under jsdom (tests) Node's undici `new Request()` needs an absolute URL,
  // so resolve against window.location when available. Both are same-origin.
  if (typeof window !== 'undefined' && window.location?.origin) {
    return `${window.location.origin}/api`;
  }
  return '/api';
})();

export const baseQuery = fetchBaseQuery({
  baseUrl: API_BASE_URL,
  credentials: 'include',
});
