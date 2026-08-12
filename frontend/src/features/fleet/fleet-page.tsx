import { toast } from 'sonner';
import { AlertTriangle, RefreshCw } from 'lucide-react';
import { useListSessionFleetQuery, useApplyConfigMutation } from '@/api/fleetApi';
import { useListPresetsQuery } from '@/api/presetsApi';
import { useSessionId } from '@/features/sessions/session-context';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { useIsOperator } from '@/hooks/useUser';
import { timeAgo } from '@/lib/time';
import { mergeInterfaces, parseScanConfig, stringifyScanConfig } from '@/lib/scanConfig';
import type { FleetDevice } from '@/lib/domain';

export function FleetPage() {
  const sessionId = useSessionId() ?? '';
  const isOperator = useIsOperator();
  const { data: fleet, isLoading, isError } = useListSessionFleetQuery(sessionId, {
    skip: !sessionId,
    pollingInterval: 5000,
  });
  const { data: presets } = useListPresetsQuery();
  const [applyConfig, { isLoading: applying }] = useApplyConfigMutation();

  const applyPreset = async (device: FleetDevice, presetId: string) => {
    const preset = presets?.find((p) => p.id === presetId);
    if (!preset) return;
    const current = parseScanConfig(device.config?.configJson);
    const merged = mergeInterfaces(parseScanConfig(preset.configJson), current.interfaces.map((i) => i.name));
    try {
      await applyConfig({
        sessionId,
        deviceId: device.device.id,
        configJson: stringifyScanConfig(merged),
        presetId: preset.id,
      }).unwrap();
      toast.success(`Preset "${preset.name}" pushed to ${device.device.name}`);
    } catch {
      toast.error(`Failed to push preset to ${device.device.name}`);
    }
  };

  const online = fleet?.filter((f) => f.device.status === 'online').length ?? 0;
  const drift = fleet?.filter((f) => f.drift).length ?? 0;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Fleet"
        description="Scanner hardware assigned to this session, their live config and push state."
        actions={
          <div className="flex items-center gap-2">
            <Badge variant="neutral">{online} online</Badge>
            <Badge variant="neutral">{drift} in drift</Badge>
          </div>
        }
      />

      {isError ? (
        <Alert variant="destructive">
          <AlertTitle>Failed to load fleet</AlertTitle>
          <AlertDescription>Check the connection and retry.</AlertDescription>
        </Alert>
      ) : null}

      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow className="bg-secondary-background">
                <TableHead>Device</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Heartbeat</TableHead>
                <TableHead>Swarm</TableHead>
                <TableHead>Config</TableHead>
                <TableHead>Preset</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow><TableCell colSpan={6} className="p-4"><Skeleton className="h-10 w-full" /></TableCell></TableRow>
              ) : null}
              {fleet?.map((f) => (
                <TableRow key={f.device.id}>
                  <TableCell>
                    <div className="font-heading">{f.device.name}</div>
                    <div className="text-xs text-foreground/60">{f.device.platform}</div>
                  </TableCell>
                  <TableCell><StatusBadge value={f.device.status} /></TableCell>
                  <TableCell className="text-xs text-foreground/60">{timeAgo(f.device.lastHeartbeatAt)}</TableCell>
                  <TableCell>
                    {f.swarmName ? (
                      <Badge variant="neutral">{f.swarmName}</Badge>
                    ) : (
                      <span className="text-xs text-foreground/60">—</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {f.config?.pushState === 'failed' ? (
                      <Badge className="bg-red-400 text-red-950">
                        <AlertTriangle className="size-3" /> push failed
                      </Badge>
                    ) : f.drift ? (
                      <Badge className="bg-amber-400 text-amber-950">
                        <AlertTriangle className="size-3" /> not acked
                      </Badge>
                    ) : (
                      <Badge className="bg-emerald-400 text-emerald-950">synced</Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    {isOperator ? (
                      <Select
                        value={f.presetId ?? 'none'}
                        onValueChange={(id: string) => applyPreset(f, id)}
                        disabled={applying}
                      >
                        <SelectTrigger className="h-8 w-44 bg-background text-foreground">
                          <SelectValue placeholder={f.presetName ?? 'No preset'} />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none" disabled>No preset</SelectItem>
                          {presets?.map((p) => (
                            <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    ) : (
                      <span className="text-sm">{f.presetName ?? '—'}</span>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {fleet?.length === 0 && !isLoading ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-sm text-foreground/60">
                    No devices assigned to this session yet.
                  </TableCell>
                </TableRow>
              ) : null}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm"><RefreshCw className="size-4" /> Live</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-foreground/70">
            Fleet status polls every 5s. Push state transitions to <code className="rounded-base border-2 border-border bg-secondary-background px-1">synced</code>{' '}
            once the scanner ACKs a config push.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
