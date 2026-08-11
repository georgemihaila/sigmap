import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export interface Me {
  authenticated: boolean;
  username: string | null;
  role: string | null;
}

export const authApi = createApi({
  reducerPath: 'authApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/api/auth' }),
  endpoints: (builder) => ({
    me: builder.query<Me, void>({
      query: () => '/me',
    }),
    login: builder.mutation<Me, { username: string; password: string }>({
      query: (body) => ({ url: '/login', method: 'POST', body }),
      async onQueryStarted(_args, { dispatch, queryFulfilled }) {
        await queryFulfilled;
        dispatch(authApi.util.invalidateTags(['me']));
      },
    }),
    logout: builder.mutation<void, void>({
      query: () => ({ url: '/logout', method: 'POST' }),
      async onQueryStarted(_args, { dispatch, queryFulfilled }) {
        await queryFulfilled;
        dispatch(authApi.util.invalidateTags(['me']));
      },
    }),
  }),
  tagTypes: ['me'],
});

export const { useMeQuery, useLoginMutation, useLogoutMutation } = authApi;
