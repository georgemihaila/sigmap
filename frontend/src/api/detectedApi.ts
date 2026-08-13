import { api } from './baseApi';
import type { DetectedDevice, DetectedDevicePage, SignalPoint } from '@/lib/domain';

export interface DetectedQuery {
  cursor?: string | null;
  limit?: number;
  deviceType?: string | null;
  search?: string | null;
}

export const detectedApi = api.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Keyset-paginated list. Accumulates pages into one query cache entry via
     * serializeQueryArgs + merge; `fetchMore` loads the next cursor.
     */
    listDetectedDevices: builder.query<DetectedDevicePage, DetectedQuery>({
      query: (args) => ({
        url: '/detected-devices',
        params: {
          limit: args.limit ?? 50,
          ...(args.cursor ? { cursor: args.cursor } : {}),
          ...(args.deviceType ? { deviceType: args.deviceType } : {}),
          ...(args.search ? { search: args.search } : {}),
        },
      }),
      serializeQueryArgs: ({ endpointName, queryArgs }) => {
        const { cursor: _cursor, ...rest } = queryArgs;
        return JSON.stringify({ endpointName, rest });
      },
      merge: (current, incoming) => {
        if (incoming.count !== undefined) current.count = incoming.count;
        const seen = new Set(current.items.map((d) => d.id));
        current.items.push(...incoming.items.filter((d) => !seen.has(d.id)));
        current.nextCursor = incoming.nextCursor;
      },
      forceRefetch: () => true,
      providesTags: ['Detected'],
    }),
    getDetectedDevice: builder.query<DetectedDevice, string>({
      query: (mac) => ({ url: `/devices/detected/${encodeURIComponent(mac)}` }),
    }),
    listMapDetectedDevices: builder.query<DetectedDevice[], void>({
      query: () => ({ url: '/detected-devices/map' }),
      providesTags: ['Detected'],
    }),
    getSignalSeries: builder.query<SignalPoint[], string>({
      query: (mac) => ({ url: `/devices/detected/${encodeURIComponent(mac)}/signal-series` }),
    }),
  }),
});

export const { useListDetectedDevicesQuery, useGetDetectedDeviceQuery, useListMapDetectedDevicesQuery, useGetSignalSeriesQuery } =
  detectedApi;
