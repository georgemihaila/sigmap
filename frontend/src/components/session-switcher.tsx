import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useListSessionsQuery } from '@/api/sessionsApi';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

/**
 * Session-context switcher, visible inside any /sessions/:sessionId/* route.
 * Switching sessions keeps the current sub-route (map/fleet/config) intact.
 */
export function SessionSwitcher() {
  const { sessionId } = useParams<{ sessionId: string }>();
  const { data: sessions } = useListSessionsQuery();
  const navigate = useNavigate();
  const location = useLocation();

  if (!sessionId) return null;

  const switchTo = (nextId: string) => {
    if (nextId === sessionId) return;
    const segments = location.pathname.split('/');
    const idx = segments.indexOf(sessionId);
    if (idx >= 0) segments[idx] = nextId;
    navigate(`${segments.join('/')}${location.search}`);
  };

  const current = sessions?.find((s) => s.id === sessionId);

  return (
    <div className="flex items-center gap-2">
      <span className="hidden text-sm font-base text-foreground/60 md:inline">Session</span>
      <Select value={sessionId} onValueChange={switchTo}>
        <SelectTrigger className="w-56 justify-start bg-background text-foreground">
          <SelectValue placeholder={current?.name ?? 'Loading…'} />
        </SelectTrigger>
        <SelectContent>
          {sessions?.map((s) => (
            <SelectItem key={s.id} value={s.id}>
              <span className="flex items-center gap-2">
                <span className="inline-block size-2 rounded-full border-2 border-border" style={{ background: s.status === 'active' ? '#34d399' : s.status === 'planned' ? '#7dd3fc' : '#a1a1aa' }} />
                {s.name}
              </span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
