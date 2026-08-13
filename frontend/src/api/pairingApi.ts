import { api } from './baseApi';
import type { PairingQr, PairingRequest } from '@/lib/domain';

export const pairingApi = api.injectEndpoints({
  endpoints: (builder) => ({
    listPendingPairings: builder.query<PairingRequest[], void>({
      query: () => ({ url: '/pairing/pending' }),
      providesTags: ['Pairing'],
    }),
    approvePairing: builder.mutation<void, { deviceId: string; sessionId?: string | null }>({
      query: ({ deviceId, sessionId }) => ({
        url: `/pairing/${deviceId}/approve`,
        method: 'POST',
        body: { sessionId: sessionId ?? null },
      }),
      invalidatesTags: ['Pairing', 'Fleet'],
    }),
    rejectPairing: builder.mutation<void, string>({
      query: (deviceId) => ({ url: `/pairing/${deviceId}/reject`, method: 'POST' }),
      invalidatesTags: ['Pairing'],
    }),
    getPairingQr: builder.query<PairingQr, void>({
      query: () => ({ url: '/pairing/qr' }),
    }),
  }),
});

export const {
  useListPendingPairingsQuery,
  useApprovePairingMutation,
  useRejectPairingMutation,
  useGetPairingQrQuery,
} = pairingApi;
