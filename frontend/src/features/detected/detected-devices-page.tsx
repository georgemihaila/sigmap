import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronDown } from 'lucide-react';
import { useListDetectedDevicesQuery, type DetectedQuery } from '@/api/detectedApi';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatDateTime, formatNumber } from '@/lib/time';
import type { DeviceType } from '@/lib/domain';

const TYPES: Array<DeviceType | 'ALL'> = ['ALL', 'AP', 'BLUETOOTH', 'BT_LE', 'CLIENT'];

export function DetectedDevicesPage() {
  const navigate = useNavigate();
  const [deviceType, setDeviceType] = useState('ALL');
  const [search, setSearch] = useState('');
  const [cursor, setCursor] = useState<string | null>(null);

  const args: DetectedQuery = {
    cursor,
    limit: 25,
    deviceType: deviceType === 'ALL' ? null : deviceType,
    search: search.trim() || null,
  };
  const { data, isLoading, isError, isFetching } = useListDetectedDevicesQuery(args);

  const changeFilters = (fn: () => void) => {
    fn();
    setCursor(null);
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Detected Devices"
        description="Every unique MAC observed across all sessions — the WiGLE-equivalent database."
      />

      <div className="flex flex-wrap items-center gap-3">
        <Select value={deviceType} onValueChange={(v: string) => changeFilters(() => setDeviceType(v))}>
          <SelectTrigger className="w-40 bg-background text-foreground">
            <SelectValue placeholder="Device type" />
          </SelectTrigger>
          <SelectContent>
            {TYPES.map((t) => (
              <SelectItem key={t} value={t}>{t === 'ALL' ? 'All types' : t}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Input
          className="w-64 bg-background"
          placeholder="Search MAC, SSID or vendor…"
          value={search}
          onChange={(e) => changeFilters(() => setSearch(e.target.value))}
        />
        <Badge variant="neutral" className="ml-auto">{formatNumber(data?.count ?? 0)} total</Badge>
      </div>

      {isError ? (
        <Alert variant="destructive">
          <AlertTitle>Failed to load detected devices</AlertTitle>
          <AlertDescription>Check the connection and retry.</AlertDescription>
        </Alert>
      ) : null}

      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow className="bg-secondary-background">
                <TableHead>SSID / Name</TableHead>
                <TableHead>MAC</TableHead>
                <TableHead>Vendor</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Last seen</TableHead>
                <TableHead className="text-right">Detections</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 6 }).map((_, i) => (
                  <TableRow key={`loading-${i}`}>
                    <TableCell colSpan={6}><Skeleton className="h-6 w-full" /></TableCell>
                  </TableRow>
                ))
              ) : null}
              {data?.items.map((d) => (
                <TableRow
                  key={d.id}
                  className="cursor-pointer hover:bg-main hover:text-main-foreground"
                  onClick={() => navigate(`/detected/${encodeURIComponent(d.mac)}`)}
                >
                  <TableCell className="font-heading">{d.ssidLatest ?? d.btNameLatest ?? '—'}</TableCell>
                  <TableCell className="font-mono text-xs">{d.mac}</TableCell>
                  <TableCell className="text-xs">{d.vendorName ?? '—'}</TableCell>
                  <TableCell><StatusBadge value={d.deviceType.toLowerCase()} label={d.deviceType} /></TableCell>
                  <TableCell className="text-xs">{formatDateTime(d.lastSeenAt)}</TableCell>
                  <TableCell className="text-right">{formatNumber(d.detectionCount)}</TableCell>
                </TableRow>
              ))}
              {data && data.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-sm text-foreground/60">
                    No devices match the current filters.
                  </TableCell>
                </TableRow>
              ) : null}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <div className="flex items-center justify-center gap-3">
        {data?.nextCursor ? (
          <Button variant="neutral" onClick={() => setCursor(data.nextCursor)} disabled={isFetching}>
            <ChevronDown /> Load more
          </Button>
        ) : data && data.items.length > 0 ? (
          <span className="text-xs text-foreground/60">End of list — you're all caught up.</span>
        ) : null}
      </div>
    </div>
  );
}
