import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export interface Session {
  id: string;
  name: string;
  description: string | null;
  startsAt: string | null;
  endsAt: string | null;
  status: number;
  createdAt: string;
}

export interface DetectedDevice {
  id: string;
  mac: string;
  macNormalized: string;
  vendorOui: string | null;
  vendorName: string | null;
  deviceType: number;
  ssidLatest: string | null;
  firstSeenAt: string;
  lastSeenAt: string;
  latitude: number | null;
  longitude: number | null;
}

export interface DetectedDevicePage {
  items: DetectedDevice[];
  nextCursor: string | null;
  count: number;
}

export interface SessionStats {
  encryptionBreakdown: Record<string, number>;
  vendorBreakdown: Record<string, number>;
  deviceTypeBreakdown: Record<string, number>;
  totalDetections: number;
  locatedDetections: number;
}

export interface ScanPreset {
  id: string;
  name: string;
  description: string | null;
  ownerId: string | null;
  configJson: string;
  isBuiltin: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Device {
  id: string;
  name: string;
  platform: string;
  capabilitiesJson: string;
  status: string;
  lastHeartbeatAt: string | null;
  lastKnownIp: string | null;
  pairedAt: string;
}

export interface DeviceConfigDto {
  configJson: string | null;
  presetId: string | null;
  source: string;
  pushState: string;
  configRev: number;
  lastPushId: string | null;
  updatedAt: string;
}

export interface ConfigPushResult {
  sessionId: string;
  deviceId: string;
  pushId: string;
  configRev: number;
  pushState: string;
}

export interface FleetConfigApplyResult {
  deviceCount: number;
  results: ConfigPushResult[];
}

export interface SignalPoint {
  at: string;
  signalDbm: number;
  channel: number;
  locationFlag: string;
  lon: number | null;
  lat: number | null;
}

export const api = createApi({
  reducerPath: 'api',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  tagTypes: ['Session', 'Device', 'Preset', 'DeviceConfig'],
  endpoints: (builder) => ({
    listSessions: builder.query<Session[], void>({
      query: () => '/sessions',
      providesTags: ['Session'],
    }),
    createSession: builder.mutation<Session, { name: string; description?: string }>({
      query: (body) => ({ url: '/sessions', method: 'POST', body }),
      invalidatesTags: ['Session'],
    }),
    deleteSession: builder.mutation<void, string>({
      query: (id) => ({ url: `/sessions/${id}`, method: 'DELETE' }),
      invalidatesTags: ['Session'],
    }),
    listDetectedDevices: builder.query<DetectedDevicePage, { cursor?: string; limit?: number }>({
      query: ({ cursor, limit = 50 }) =>
        `/devices/detected?limit=${limit}${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`,
      serializeQueryArgs: ({ endpointName }) => endpointName,
      merge: (current, incoming) => {
        const seen = new Set(current.items.map((d) => d.macNormalized));
        current.items.push(...incoming.items.filter((d) => !seen.has(d.macNormalized)));
        current.nextCursor = incoming.nextCursor;
      },
      forceRefetch: () => true,
    }),
    getSessionStats: builder.query<SessionStats, string>({
      query: (sessionId) => `/sessions/${sessionId}/stats`,
    }),
    listDevices: builder.query<Device[], void>({
      query: () => '/devices',
      providesTags: ['Device'],
    }),
    listPresets: builder.query<ScanPreset[], void>({
      query: () => '/presets',
      providesTags: ['Preset'],
    }),
    createPreset: builder.mutation<ScanPreset, { name: string; description?: string; configJson: string }>({
      query: (body) => ({ url: '/presets', method: 'POST', body }),
      invalidatesTags: ['Preset'],
    }),
    updatePreset: builder.mutation<ScanPreset, { id: string; body: Partial<ScanPreset> }>({
      query: ({ id, body }) => ({ url: `/presets/${id}`, method: 'PUT', body }),
      invalidatesTags: ['Preset'],
    }),
    deletePreset: builder.mutation<void, string>({
      query: (id) => ({ url: `/presets/${id}`, method: 'DELETE' }),
      invalidatesTags: ['Preset'],
    }),
    getDeviceConfig: builder.query<DeviceConfigDto, { sessionId: string; deviceId: string }>({
      query: ({ sessionId, deviceId }) => `/sessions/${sessionId}/devices/${deviceId}/config`,
      providesTags: (_result, _error, arg) => [
        { type: 'DeviceConfig' as const, id: `${arg.sessionId}:${arg.deviceId}` },
      ],
    }),
    applyConfig: builder.mutation<ConfigPushResult, { sessionId: string; deviceId: string; config: string; presetId: string | null }>({
      query: ({ sessionId, deviceId, config, presetId }) => ({
        url: `/sessions/${sessionId}/devices/${deviceId}/config`,
        method: 'PUT',
        body: { config, presetId },
      }),
      invalidatesTags: (result) =>
        result
          ? [{ type: 'DeviceConfig' as const, id: `${result.sessionId}:${result.deviceId}` }]
          : [],
    }),
    applyFleetConfig: builder.mutation<FleetConfigApplyResult, { sessionId: string; config: string; presetId: string | null }>({
      query: ({ sessionId, config, presetId }) => ({
        url: `/sessions/${sessionId}/devices/config`,
        method: 'PUT',
        body: { config, presetId },
      }),
      invalidatesTags: ['DeviceConfig'],
    }),
    getSignalSeries: builder.query<SignalPoint[], string>({
      query: (mac) => `/devices/detected/${encodeURIComponent(mac)}/signal-series`,
    }),
    getDetectedDeviceDetail: builder.query<unknown, string>({
      query: (mac) => `/devices/detected/${encodeURIComponent(mac)}`,
    }),
    listPendingPairings: builder.query<Array<{ id: string; name: string; platform: string; pairedAt: string }>, void>({
      query: () => '/pairing/pending',
      providesTags: ['Device'],
    }),
    approvePairing: builder.mutation<unknown, { deviceId: string; sessionId: string }>({
      query: ({ deviceId, sessionId }) => ({
        url: `/pairing/${deviceId}/approve`,
        method: 'POST',
        body: { sessionId },
      }),
      invalidatesTags: ['Device'],
    }),
    rejectPairing: builder.mutation<void, string>({
      query: (deviceId) => ({ url: `/pairing/${deviceId}/reject`, method: 'POST' }),
      invalidatesTags: ['Device'],
    }),
    pairingQr: builder.query<string, string>({
      query: (sessionId) => `/pairing/qr/${sessionId}`,
    }),
    listExports: builder.query<Array<{ id: string; sessionId: string | null; format: string; status: string; createdAt: string }>, void>({
      query: () => '/exports',
    }),
    createExport: builder.mutation<unknown, { sessionId: string | null; format: string }>({
      query: (body) => ({ url: '/exports', method: 'POST', body }),
    }),
    uploadWigle: builder.mutation<{ status: string; message: string | null }, { sessionId: string }>({
      query: (body) => ({ url: '/wigle/upload', method: 'POST', body }),
    }),
    importWigle: builder.mutation<number, void>({
      query: () => ({ url: '/wigle/import', method: 'POST' }),
    }),
    getWigleSettings: builder.query<{ apiName: string | null; apiKeySet: boolean; username: string | null; passwordSet: boolean } | null, void>({
      query: () => '/settings/wigle',
    }),
    putWigleSettings: builder.mutation<unknown, { apiName?: string; apiKey?: string; username?: string; password?: string }>({
      query: (body) => ({ url: '/settings/wigle', method: 'PUT', body }),
    }),
    getCoverage: builder.query<{ sessionId: string; points: Array<{ lat: number; lon: number }>; count: number }, string>({
      query: (sessionId) => `/sessions/${sessionId}/coverage?limit=5000`,
    }),
    compareSessions: builder.query<{ onlyInA: number; onlyInB: number; inBoth: number; totalA: number; totalB: number }, { a: string; b: string }>({
      query: ({ a, b }) => `/sessions/compare?a=${a}&b=${b}`,
    }),
    importGpx: builder.mutation<number, { sessionId: string; xml: string }>({
      query: ({ sessionId, xml }) => ({
        url: `/sessions/${sessionId}/gpx`,
        method: 'POST',
        body: xml,
        headers: { 'Content-Type': 'application/gpx+xml' },
      }),
    }),
  }),
});

export const {
  useListSessionsQuery,
  useCreateSessionMutation,
  useDeleteSessionMutation,
  useListDetectedDevicesQuery,
  useGetSessionStatsQuery,
  useListDevicesQuery,
  useListPresetsQuery,
  useCreatePresetMutation,
  useUpdatePresetMutation,
  useDeletePresetMutation,
  useGetDeviceConfigQuery,
  useApplyConfigMutation,
  useApplyFleetConfigMutation,
  useGetSignalSeriesQuery,
  useGetDetectedDeviceDetailQuery,
  useListPendingPairingsQuery,
  useApprovePairingMutation,
  useRejectPairingMutation,
  usePairingQrQuery,
  useListExportsQuery,
  useCreateExportMutation,
  useUploadWigleMutation,
  useImportWigleMutation,
  useGetWigleSettingsQuery,
  usePutWigleSettingsMutation,
  useGetCoverageQuery,
  useCompareSessionsQuery,
  useImportGpxMutation,
} = api;
