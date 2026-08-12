import { api } from './baseApi';
import type { ConfigDiff, ConfigPushResult, ScanPreset } from '@/lib/domain';

export const presetsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    listPresets: builder.query<ScanPreset[], void>({
      query: () => ({ url: '/presets' }),
      providesTags: ['Preset'],
    }),
    createPreset: builder.mutation<ScanPreset, { name: string; description?: string; configJson: string }>({
      query: (body) => ({ url: '/presets', method: 'POST', body }),
      invalidatesTags: ['Preset'],
    }),
    updatePreset: builder.mutation<ScanPreset, { id: string; body: { name?: string; description?: string; configJson?: string } }>({
      query: ({ id, body }) => ({ url: `/presets/${id}`, method: 'PUT', body }),
      invalidatesTags: ['Preset'],
    }),
    deletePreset: builder.mutation<void, string>({
      query: (id) => ({ url: `/presets/${id}`, method: 'DELETE' }),
      invalidatesTags: ['Preset'],
    }),
    /** Diff a preset's config against a set of devices' live configs. */
    previewPresetOnFleet: builder.query<ConfigDiff[], { presetId: string; sessionId: string }>({
      query: ({ presetId, sessionId }) => ({
        url: `/sessions/${sessionId}/presets/${presetId}/preview`,
      }),
    }),
    /** Apply a preset to one or more devices in a session. */
    applyPresetToDevices: builder.mutation<
      ConfigPushResult[],
      { sessionId: string; presetId: string; deviceIds: string[] }
    >({
      query: ({ sessionId, presetId, deviceIds }) => ({
        url: `/sessions/${sessionId}/presets/${presetId}/apply`,
        method: 'POST',
        body: { deviceIds },
      }),
      invalidatesTags: ['Fleet', 'Config'],
    }),
  }),
});

export const {
  useListPresetsQuery,
  useCreatePresetMutation,
  useUpdatePresetMutation,
  useDeletePresetMutation,
  usePreviewPresetOnFleetQuery,
  useApplyPresetToDevicesMutation,
} = presetsApi;
