import { api } from '@/api/baseApi';
import type { LiveSessionState } from '@/lib/domain';

/** Live session state, fed by the stream provider via `updateQueryData`. */
export const liveApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getLiveSession: builder.query<LiveSessionState, string>({
      query: (sessionId) => ({ url: `/sessions/${sessionId}/live` }),
    }),
  }),
});

export const { useGetLiveSessionQuery } = liveApi;
