import { api } from './baseApi';
import type { ConfigPushResult, DeviceConfigDto, FleetDevice } from '@/lib/domain';

export const fleetApi = api.injectEndpoints({
  endpoints: (builder) => ({
    listFleet: builder.query<FleetDevice[], void>({
      query: () => ({ url: '/fleet' }),
      providesTags: ['Fleet'],
    }),
    listSessionFleet: builder.query<FleetDevice[], string>({
      query: (sessionId) => ({ url: `/sessions/${sessionId}/fleet` }),
      providesTags: ['Fleet'],
    }),
    getDeviceConfig: builder.query<DeviceConfigDto, { sessionId: string; deviceId: string }>({
      query: ({ sessionId, deviceId }) => ({
        url: `/sessions/${sessionId}/devices/${deviceId}/config`,
      }),
      providesTags: (_result, _error, arg) => [
        { type: 'Config', id: `${arg.sessionId}:${arg.deviceId}` },
      ],
    }),
    applyConfig: builder.mutation<
      ConfigPushResult,
      { sessionId: string; deviceId: string; configJson: string; presetId?: string | null }
    >({
      query: ({ sessionId, deviceId, ...body }) => ({
        url: `/sessions/${sessionId}/devices/${deviceId}/config`,
        method: 'PUT',
        body,
      }),
      invalidatesTags: (_result, _error, arg) => [
        { type: 'Config', id: `${arg.sessionId}:${arg.deviceId}` },
        'Fleet',
      ],
    }),
    getPushStatus: builder.query<
      { pushState: DeviceConfigDto['pushState']; lastPushId: string | null; configRev: number },
      { sessionId: string; deviceId: string }
    >({
      query: ({ sessionId, deviceId }) => ({
        url: `/sessions/${sessionId}/devices/${deviceId}/config/push-status`,
      }),
    }),
  }),
});

export const {
  useListFleetQuery,
  useListSessionFleetQuery,
  useGetDeviceConfigQuery,
  useApplyConfigMutation,
  useGetPushStatusQuery,
} = fleetApi;
