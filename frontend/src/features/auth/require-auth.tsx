import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useGetMeQuery } from '@/api/authApi';
import { Skeleton } from '@/components/ui/skeleton';

export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: user, isLoading } = useGetMeQuery();
  const location = useLocation();

  if (isLoading) {
    return (
      <div className="flex min-h-svh flex-col gap-3 p-8">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-6 w-72" />
        <Skeleton className="mt-4 h-64 w-full" />
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  return <>{children}</>;
}
