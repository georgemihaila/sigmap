import { createApi } from '@reduxjs/toolkit/query/react';
import { baseQuery } from './baseQuery';

export const tagTypes = [
  'Auth',
  'Session',
  'Fleet',
  'Preset',
  'Detected',
  'Config',
  'Export',
  'Pairing',
] as const;

export type TagType = (typeof tagTypes)[number];

/**
 * Single RTK Query API, one baseQuery, one reducerPath. Domain slices extend
 * it with `injectEndpoints` so the baseUrl seam stays in exactly one place.
 */
export const api = createApi({
  reducerPath: 'api',
  baseQuery,
  tagTypes,
  endpoints: () => ({}),
});
