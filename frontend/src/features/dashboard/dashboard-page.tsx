import { Link } from 'react-router-dom';
import { ArrowRight, Radio, ShieldCheck, WifiOff, TriangleAlert, Clock } from 'lucide-react';
import { useListSessionsQuery } from '@/api/sessionsApi';
import { useListFleetQuery } from '@/api/fleetApi';
import { useListDetectedDevicesQuery } from '@/api/detectedApi';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { timeAgo } from '@/lib/time';
import { cn } from '@/lib/utils';

export function DashboardPage() {
  const { data: sessions, isError: sessionsError } = useListSessionsQuery();
  const { data: fleet, isError: fleetError } = useListFleetQuery();
  const { data: recent, isError: recentError } = useListDetectedDevicesQuery({ limit: 8 });

  const active = sessions?.filter((s) => s.status === 'active') ?? [];
  const planned = sessions?.filter((s) => s.status === 'planned') ?? [];

  const online = fleet?.filter((f) => f.device.status === 'online').length ?? 0;
  const offline = fleet?.filter((f) => f.device.status === 'offline').length ?? 0;
  const errors = fleet?.filter((f) => f.device.status === 'error').length ?? 0;
  const pending = fleet?.filter((f) => f.device.status === 'pending').length ?? 0;
  const drift = fleet?.filter((f) => f.drift).length ?? 0;

  const stats = [
    { label: 'Online', value: online, className: 'bg-emerald-400 text-emerald-950', icon: Radio },
    { label: 'Offline', value: offline, className: 'bg-zinc-300 text-zinc-800', icon: WifiOff },
    { label: 'Errors', value: errors, className: 'bg-red-400 text-red-950', icon: TriangleAlert },
    { label: 'Pending pairing', value: pending, className: 'bg-amber-400 text-amber-950', icon: Clock },
    { label: 'Config drift', value: drift, className: 'bg-main text-main-foreground', icon: ShieldCheck },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Dashboard" description="Operational overview of sessions and fleet health." />

      {sessionsError || fleetError || recentError ? (
        <Alert variant="destructive">
          <AlertTitle>Some data failed to load</AlertTitle>
          <AlertDescription>Session, fleet or detection data is unavailable right now.</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid grid-cols-2 gap-4 md:grid-cols-5">
        {stats.map((s) => {
          const Icon = s.icon;
          return (
            <Card key={s.label} className="gap-3 py-4">
              <CardHeader className="flex-row items-center gap-2 px-4">
                <span className={cn('flex size-8 items-center justify-center rounded-base border-2 border-border', s.className)}>
                  <Icon className="size-4" />
                </span>
                <CardTitle className="text-sm">{s.label}</CardTitle>
              </CardHeader>
              <CardContent className="px-4 py-0">
                <div className="font-heading text-3xl">{s.value}</div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Active sessions</CardTitle>
            <CardDescription>Live ops and upcoming sweeps.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {active.length === 0 && planned.length === 0 ? (
              <p className="text-sm text-foreground/60">No sessions yet — create one to get started.</p>
            ) : null}
            {active.map((s) => (
              <Link key={s.id} to={`/sessions/${s.id}`}>
                <div className="flex items-center justify-between gap-3 rounded-base border-2 border-border bg-secondary-background p-3 transition-transform hover:-translate-y-0.5 hover:shadow-shadow">
                  <div className="flex min-w-0 flex-col gap-1">
                    <span className="truncate font-base font-heading">{s.name}</span>
                    <span className="text-xs text-foreground/60">{s.description ?? '—'}</span>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    <StatusBadge value={s.status} />
                    <ArrowRight className="size-4" />
                  </div>
                </div>
              </Link>
            ))}
            {planned.slice(0, 3).map((s) => (
              <Link key={s.id} to={`/sessions/${s.id}`}>
                <div className="flex items-center justify-between gap-3 rounded-base border-2 border-dashed border-border p-3">
                  <span className="truncate font-base">{s.name}</span>
                  <StatusBadge value={s.status} />
                </div>
              </Link>
            ))}
            <Button variant="neutral" asChild className="self-start">
              <Link to="/sessions">Manage sessions</Link>
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recently detected</CardTitle>
            <CardDescription>Latest heard devices across all sessions.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {recent?.items.map((d) => (
              <Link key={d.id} to={`/detected/${encodeURIComponent(d.mac)}`}>
                <div className="flex items-center justify-between gap-3 rounded-base border-2 border-border bg-secondary-background p-2.5 transition-transform hover:-translate-y-0.5 hover:shadow-shadow">
                  <div className="flex min-w-0 flex-col">
                    <span className="truncate font-heading text-sm">{d.ssidLatest ?? d.btNameLatest ?? d.mac}</span>
                    <span className="truncate text-xs text-foreground/60">{d.mac}</span>
                  </div>
                  <div className="flex shrink-0 flex-col items-end gap-1">
                    <Badge variant="neutral">{d.vendorName ?? d.deviceType}</Badge>
                    <span className="text-xs text-foreground/60">{timeAgo(d.lastSeenAt)}</span>
                  </div>
                </div>
              </Link>
            ))}
            {recent && recent.items.length === 0 ? (
              <p className="text-sm text-foreground/60">No detections yet.</p>
            ) : null}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
