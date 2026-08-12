import { api } from './baseApi';
import type { User } from '@/lib/domain';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  user: User;
}

export const authApi = api.injectEndpoints({
  endpoints: (builder) => ({
    getMe: builder.query<User | null, void>({
      query: () => ({ url: '/auth/me', validateStatus: (response, _result) => {
        if (response.status === 401) return true;
        return response.status >= 200 && response.status < 300;
      } }),
      transformResponse: (response: { user?: User } | null) => (response?.user ?? null),
      providesTags: ['Auth'],
    }),
    login: builder.mutation<LoginResponse, LoginRequest>({
      query: (body) => ({ url: '/auth/login', method: 'POST', body }),
      invalidatesTags: ['Auth'],
    }),
    logout: builder.mutation<void, void>({
      query: () => ({ url: '/auth/logout', method: 'POST' }),
      invalidatesTags: ['Auth'],
    }),
  }),
});

export const { useGetMeQuery, useLoginMutation, useLogoutMutation } = authApi;
