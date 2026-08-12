import { cn } from '@/lib/utils';
import { useGetSessionQuery, useArchiveSessionMutation } from '@/api/sessionsApi';
import { Outlet, useLocation, useNavigate, useParams } from 'react-router-dom';
import { LiveStreamProvider } from '@/live/LiveStreamProvider';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertTitle } from '@/components/ui/alert';
import { useIsOperator } from '@/hooks/useUser';
import { SessionContext } from './session-context';

const TABS = [
  { key: 'overview', label: 'Overview', path: '' },
  { key: 'map', label: 'Live Map', path: 'map' },
  { key: 'fleet', label: 'Fleet', path: 'fleet' },
  { key: 'config', label: 'Config', path: 'config' },
] as const;

export function SessionLayout() {
  const { sessionId = '' } = useParams();
  const { data: session, isLoading, isError } = useGetSessionQuery(sessionId, { skip: !sessionId });
  const [archive] = useArchiveSessionMutation();
  const navigate = useNavigate();
  const location = useLocation();
  const isOperator = useIsOperator();

  if (!sessionId) return null;

  if (isLoading) {
    return (
      <div className="flex flex-col gap-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-8 w-full" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  if (isError || !session) {
    return (
      <Alert>
        <AlertTitle>Session not found</AlertTitle>
        <Button variant="neutral" onClick={() => navigate('/sessions')} className="mt-2">
          Back to sessions
        </Button>
      </Alert>
    );
  }

  const segment = location.pathname.split('/').slice(3).join('/');
  const active = TABS.find((t) => t.path === segment)?.key ?? 'overview';

  const select = (key: string) => {
    const tab = TABS.find((t) => t.key === key);
    if (!tab) return;
    navigate(`/sessions/${sessionId}${tab.path ? `/${tab.path}` : ''}`);
  };

  return (
    <SessionContext.Provider value={sessionId}>
      <LiveStreamProvider sessionId={sessionId}>
        <div className="flex flex-col gap-4">
          <PageHeader
            title={
              <span className="flex items-center gap-3">
                {session.name}
                <StatusBadge value={session.status} />
              </span>
            }
            description={session.description ?? 'No description'}
            actions={
              session.status === 'active' && isOperator ? (
                <Button
                  variant="neutral"
                  onClick={async () => {
                    await archive(sessionId);
                  }}
                >
                  Archive session
                </Button>
              ) : null
            }
          />
          <nav className="flex flex-wrap gap-2 border-b-2 border-border pb-2">
            {TABS.map((tab) => (
              <Button
                key={tab.key}
                variant={active === tab.key ? 'default' : 'noShadow'}
                className={cn(active !== tab.key && 'bg-transparent text-foreground shadow-none hover:bg-main')}
                onClick={() => select(tab.key)}
              >
                {tab.label}
              </Button>
            ))}
          </nav>
          <Outlet />
        </div>
      </LiveStreamProvider>
    </SessionContext.Provider>
  );
}
