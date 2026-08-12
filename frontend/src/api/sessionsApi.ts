import { api } from './baseApi';
import type { Coverage, Session, SessionStats, Swarm } from '@/lib/domain';

export interface SessionInput {
  name: string;
  description?: string | null;
  startsAt?: string | null;
  endsAt?: string | null;
}

export const sessionsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    listSessions: builder.query<Session[], void>({
      query: () => ({ url: '/sessions' }),
      providesTags: ['Session'],
    }),
    getSession: builder.query<Session, string>({
      query: (id) => ({ url: `/sessions/${id}` }),
      providesTags: (_result, _error, id) => [{ type: 'Session', id }],
    }),
    createSession: builder.mutation<Session, SessionInput>({
      query: (body) => ({ url: '/sessions', method: 'POST', body }),
      invalidatesTags: ['Session'],
    }),
    updateSession: builder.mutation<Session, { id: string; body: Partial<SessionInput> }>({
      query: ({ id, body }) => ({ url: `/sessions/${id}`, method: 'PUT', body }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: 'Session', id },
        'Session',
      ],
    }),
    archiveSession: builder.mutation<Session, string>({
      query: (id) => ({ url: `/sessions/${id}/archive`, method: 'POST' }),
      invalidatesTags: ['Session'],
    }),
    getSessionStats: builder.query<SessionStats, string>({
      query: (id) => ({ url: `/sessions/${id}/stats` }),
    }),
    listSwarms: builder.query<Swarm[], string>({
      query: (sessionId) => ({ url: `/sessions/${sessionId}/swarms` }),
    }),
    getCoverage: builder.query<Coverage, string>({
      query: (sessionId) => ({ url: `/sessions/${sessionId}/coverage`, params: { limit: 5000 } }),
    }),
  }),
});

export const {
  useListSessionsQuery,
  useGetSessionQuery,
  useCreateSessionMutation,
  useUpdateSessionMutation,
  useArchiveSessionMutation,
  useGetSessionStatsQuery,
  useListSwarmsQuery,
  useGetCoverageQuery,
} = sessionsApi;
