import { useGetSessionStatsQuery, useListSwarmsQuery } from '@/api/sessionsApi';
import { useListSessionFleetQuery } from '@/api/fleetApi';
import { useSessionId } from '@/features/sessions/session-context';
import { PageHeader } from '@/components/page-header';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { formatNumber } from '@/lib/time';
import { cn } from '@/lib/utils';
import { Radio } from 'lucide-react';

function Bar({ value, max }: { value: number; max: number }) {
  const pct = max > 0 ? Math.max(3, Math.round((value / max) * 100)) : 0;
  return (
    <div className="h-3 w-full rounded-base border-2 border-border bg-secondary-background">
      <div className="h-full rounded-sm bg-main" style={{ width: `${pct}%` }} />
    </div>
  );
}

export function SessionOverviewPage() {
  const sessionId = useSessionId() ?? '';
  const { data: stats, isLoading, isError } = useGetSessionStatsQuery(sessionId, { skip: !sessionId });
  const { data: swarms } = useListSwarmsQuery(sessionId, { skip: !sessionId });
  const { data: fleet } = useListSessionFleetQuery(sessionId, { skip: !sessionId });

  if (isLoading) {
    return (
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-24" />)}
      </div>
    );
  }

  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Failed to load session stats</AlertTitle>
        <AlertDescription>Check the connection and retry.</AlertDescription>
      </Alert>
    );
  }

  const typeMax = Math.max(1, ...Object.values(stats?.deviceTypeBreakdown ?? {}));
  const encMax = Math.max(1, ...Object.values(stats?.encryptionBreakdown ?? {}));

  const statCards = [
    { label: 'Total detections', value: stats?.totalDetections ?? 0 },
    { label: 'Located (GPS)', value: stats?.locatedDetections ?? 0 },
    { label: 'Unlocated', value: stats?.unlocatedDetections ?? 0 },
    { label: 'Devices in session', value: stats?.deviceCount ?? 0 },
    { label: 'Devices online', value: stats?.onlineDevices ?? 0 },
    { label: 'Swarms', value: swarms?.length ?? 0 },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Overview" description="Session statistics and coverage summary." />

      <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
        {statCards.map((c) => (
          <Card key={c.label} className="gap-2 py-4">
            <CardHeader className="px-4 py-0">
              <CardTitle className="text-xs text-foreground/70">{c.label}</CardTitle>
            </CardHeader>
            <CardContent className="px-4 py-0">
              <div className="font-heading text-3xl">{formatNumber(c.value)}</div>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><Radio className="size-4" /> Device types</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {(Object.entries(stats?.deviceTypeBreakdown ?? {}) as Array<[string, number]>)
              .sort((a, b) => b[1] - a[1])
              .map(([type, count]) => (
                <div key={type} className="flex flex-col gap-1">
                  <div className="flex items-center justify-between text-sm">
                    <span className="font-base">{type}</span>
                    <span className="text-foreground/70">{formatNumber(count)}</span>
                  </div>
                  <Bar value={count} max={typeMax} />
                </div>
              ))}
            {!stats?.deviceTypeBreakdown || Object.keys(stats.deviceTypeBreakdown).length === 0 ? (
              <p className="text-sm text-foreground/60">No detections yet.</p>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Encryption breakdown</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {(Object.entries(stats?.encryptionBreakdown ?? {}) as Array<[string, number]>)
              .sort((a, b) => b[1] - a[1])
              .map(([enc, count]) => (
                <div key={enc} className="flex flex-col gap-1">
                  <div className="flex items-center justify-between text-sm">
                    <span className="font-base capitalize">{enc}</span>
                    <span className="text-foreground/70">{formatNumber(count)}</span>
                  </div>
                  <Bar value={count} max={encMax} />
                </div>
              ))}
            {!stats?.encryptionBreakdown || Object.keys(stats.encryptionBreakdown).length === 0 ? (
              <p className="text-sm text-foreground/60">No AP detections yet.</p>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Top vendors</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow className="bg-secondary-background">
                  <TableHead>Vendor</TableHead>
                  <TableHead className="text-right">Detections</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {Object.entries(stats?.vendorBreakdown ?? {}).map(([vendor, count]) => (
                  <TableRow key={vendor}>
                    <TableCell className="font-base">{vendor}</TableCell>
                    <TableCell className="text-right">{formatNumber(count)}</TableCell>
                  </TableRow>
                ))}
                {!stats?.vendorBreakdown || Object.keys(stats.vendorBreakdown).length === 0 ? (
                  <TableRow><TableCell colSpan={2} className="text-sm text-foreground/60">No vendor data.</TableCell></TableRow>
                ) : null}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Swarms</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {swarms?.map((w) => (
              <div
                key={w.id}
                className={cn(
                  'flex items-center justify-between rounded-base border-2 border-border p-3',
                  w.name === 'Alpha' ? 'bg-main text-main-foreground' : 'bg-secondary-background',
                )}
              >
                <div>
                  <div className="font-heading">{w.name}</div>
                  <div className="text-xs text-foreground/60">swarm</div>
                </div>
                <div className="text-sm">
                  {fleet?.filter((f) => f.swarmId === w.id).length ?? 0} devices
                </div>
              </div>
            ))}
            {!swarms?.length ? <p className="text-sm text-foreground/60">No swarms for this session.</p> : null}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
