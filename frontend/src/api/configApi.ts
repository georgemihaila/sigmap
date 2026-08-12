import { api } from './baseApi';
import type { ConfigDiff, ConfigPushResult } from '@/lib/domain';

/** Bulk session config edit: preview the per-device diff before pushing. */
export const configApi = api.injectEndpoints({
  endpoints: (builder) => ({
    previewSessionConfig: builder.mutation<ConfigDiff[], { sessionId: string; configJson: string }>({
      query: ({ sessionId, configJson }) => ({
        url: `/sessions/${sessionId}/config/preview`,
        method: 'POST',
        body: { configJson },
      }),
    }),
    applySessionConfig: builder.mutation<
      ConfigPushResult[],
      { sessionId: string; configJson: string; deviceIds?: string[] }
    >({
      query: ({ sessionId, ...body }) => ({
        url: `/sessions/${sessionId}/config`,
        method: 'PUT',
        body,
      }),
      invalidatesTags: ['Fleet', 'Config'],
    }),
  }),
});

export const { usePreviewSessionConfigMutation, useApplySessionConfigMutation } = configApi;
