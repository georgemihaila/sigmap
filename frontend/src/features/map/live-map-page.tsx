import { Link } from 'react-router-dom';
import { CircleAlert, Radio, Waypoints } from 'lucide-react';
import { useGetCoverageQuery } from '@/api/sessionsApi';
import { useGetLiveSessionQuery } from '@/live/liveApi';
import { useLiveStream } from '@/live/LiveStreamProvider';
import { useSessionId } from '@/features/sessions/session-context';
import { LiveMap } from '@/components/live-map';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { ScrollArea } from '@/components/ui/scroll-area';
import { formatNumber, timeAgo } from '@/lib/time';
import { cn } from '@/lib/utils';

export function LiveMapPage() {
  const sessionId = useSessionId() ?? '';
  const { connected } = useLiveStream();
  const { data: live, isLoading } = useGetLiveSessionQuery(sessionId, { skip: !sessionId });
  const { data: coverage } = useGetCoverageQuery(sessionId, { skip: !sessionId });

  const points = Object.values(live?.points ?? {});
  const locatedCount = new Set(points.map((p) => p.mac)).size;

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Live Map"
        description="Located detections streaming in from the fleet, clustered by device type."
        actions={
          connected ? (
            <Badge className="bg-emerald-400 text-emerald-950"><Radio className="size-3" /> Live</Badge>
          ) : (
            <Badge variant="neutral">connecting…</Badge>
          )
        }
      />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader className="flex-row items-center justify-between">
            <CardTitle className="flex items-center gap-2 text-sm"><Waypoints className="size-4" /> Coverage &amp; detections</CardTitle>
            <span className="text-xs text-foreground/60">
              {formatNumber(locatedCount)} located · {live?.unlocated.length ?? 0} unlocated
            </span>
          </CardHeader>
          <CardContent className="p-0">
            <div className="h-[62vh]">
              {isLoading || !live ? <Skeleton className="h-full w-full" /> : <LiveMap state={live} coverage={coverage} />}
            </div>
          </CardContent>
        </Card>

        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-sm"><Radio className="size-4" /> Fleet heartbeats</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              {Object.values(live?.devices ?? {}).map((hb) => (
                <div key={hb.deviceId} className="flex items-center justify-between rounded-base border-2 border-border bg-secondary-background px-3 py-2 text-sm">
                  <span className="font-base">{hb.deviceId.slice(0, 8)}</span>
                  <StatusBadge value={hb.status} />
                </div>
              ))}
              {!live?.devices || Object.keys(live.devices).length === 0 ? (
                <p className="text-sm text-foreground/60">No device heartbeats yet.</p>
              ) : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-sm">
                <CircleAlert className="size-4" /> Unlocated detections
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              <ScrollArea className="h-56 px-0">
                <div className="flex flex-col gap-1.5 px-3 pb-3">
                  {live?.unlocated.length === 0 ? (
                    <p className="px-1 text-sm text-foreground/60">
                      No unlocated detections. Detections without GPS evidence are never given a fabricated position.
                    </p>
                  ) : null}
                  {live?.unlocated.map((d) => (
                    <div
                      key={`${d.mac}-${d.detectedAt}`}
                      className={cn(
                        'flex items-center justify-between gap-2 rounded-base border-2 border-dashed border-border bg-secondary-background px-3 py-1.5 text-sm',
                      )}
                    >
                      <div className="min-w-0">
                        <div className="truncate font-base">{d.ssid ?? d.btName ?? d.mac}</div>
                        <div className="truncate text-xs text-foreground/60">{d.mac}</div>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        <span className="text-xs">{d.signalDbm} dBm</span>
                        <Link to={`/detected/${encodeURIComponent(d.mac)}`} className="text-xs underline">detail</Link>
                      </div>
                    </div>
                  ))}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm">Batch feed</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center justify-between text-sm text-foreground/70">
                <span>Batches received</span>
                <span className="font-heading">{formatNumber(live?.batchCount ?? 0)}</span>
              </div>
              <div className="mt-1 flex items-center justify-between text-sm text-foreground/70">
                <span>Last batch</span>
                <span>{live?.lastBatchAt ? timeAgo(live.lastBatchAt) : '—'}</span>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
