import { useGetMeQuery } from '@/api/authApi';

export function useUser() {
  const { data: user } = useGetMeQuery();
  return user;
}

/** Operator role can push config and manage devices; viewers are read-only. */
export function useIsOperator(): boolean {
  return useUser()?.role === 'operator';
}
