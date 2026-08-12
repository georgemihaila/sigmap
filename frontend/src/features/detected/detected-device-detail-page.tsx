import { Link, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { CartesianGrid, Line, LineChart, XAxis, YAxis } from 'recharts';
import { useGetDetectedDeviceQuery, useGetSignalSeriesQuery } from '@/api/detectedApi';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatDateTime, formatNumber, formatTime } from '@/lib/time';

const chartConfig: ChartConfig = {
  signal: { label: 'Signal (dBm)', color: 'var(--color-chart-1)' },
};

export function DetectedDeviceDetailPage() {
  const { mac = '' } = useParams();
  const decoded = decodeURIComponent(mac);
  const { data: device, isLoading, isError } = useGetDetectedDeviceQuery(decoded);
  const { data: series } = useGetSignalSeriesQuery(decoded);

  if (isLoading) {
    return (
      <div className="flex flex-col gap-4">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex flex-col gap-4">
        <Button variant="neutral" asChild className="self-start"><Link to="/detected"><ArrowLeft /> Back</Link></Button>
        <Alert variant="destructive">
          <AlertTitle>Failed to load device</AlertTitle>
          <AlertDescription>Check the connection and retry.</AlertDescription>
        </Alert>
      </div>
    );
  }

  if (!device) {
    return (
      <div className="flex flex-col gap-4">
        <Button variant="neutral" asChild className="self-start"><Link to="/detected"><ArrowLeft /> Back</Link></Button>
        <p className="text-sm text-foreground/60">Device not found.</p>
      </div>
    );
  }

  const chartData = (series ?? []).map((p) => ({
    time: formatTime(p.at),
    signal: p.signalDbm,
    flag: p.locationFlag,
  }));

  const located = (series ?? []).filter((p) => p.locationFlag !== 'unlocated').length;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <span className="font-mono text-xl">{device.mac}</span>
            <StatusBadge value={device.deviceType.toLowerCase()} label={device.deviceType} />
          </span>
        }
        description={
          device.ssidLatest ?? device.btNameLatest
            ? `${device.ssidLatest ?? ''}${device.btNameLatest ? ` · ${device.btNameLatest}` : ''}`
            : 'No recent SSID / name'
        }
        actions={
          <Button variant="neutral" asChild><Link to="/detected"><ArrowLeft /> Back</Link></Button>
        }
      />

      <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
        {[
          ['Vendor', device.vendorName ?? '—'],
          ['First seen', formatDateTime(device.firstSeenAt)],
          ['Last seen', formatDateTime(device.lastSeenAt)],
          ['Channel', device.channelLatest != null ? String(device.channelLatest) : '—'],
          ['Detections', formatNumber(device.detectionCount)],
          ['Location', device.latitude != null && device.longitude != null ? `${device.latitude.toFixed(5)}, ${device.longitude.toFixed(5)}` : 'unlocated'],
        ].map(([label, value]) => (
          <Card key={label} className="gap-2 py-4">
            <CardHeader className="px-4 py-0"><CardTitle className="text-xs text-foreground/70">{label}</CardTitle></CardHeader>
            <CardContent className="px-4 py-0"><div className="text-sm font-base">{value}</div></CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Signal over time</CardTitle>
          <CardDescription>
            {chartData.length} observations — {located} located ({series?.length ?? 0} total).
          </CardDescription>
        </CardHeader>
        <CardContent>
          {chartData.length === 0 ? (
            <p className="text-sm text-foreground/60">No signal history for this device.</p>
          ) : (
            <ChartContainer config={chartConfig} className="h-64 w-full">
              <LineChart data={chartData}>
                <CartesianGrid vertical={false} />
                <XAxis dataKey="time" tickLine={false} axisLine={false} tickMargin={8} minTickGap={24} />
                <YAxis tickLine={false} axisLine={false} domain={[-100, -20]} />
                <ChartTooltip content={<ChartTooltipContent />} />
                <Line type="monotone" dataKey="signal" stroke="var(--color-chart-1)" strokeWidth={2} dot={false} />
              </LineChart>
            </ChartContainer>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Recent detections</CardTitle>
          <CardDescription>Location resolution flag for each observation.</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow className="bg-secondary-background">
                <TableHead>Time</TableHead>
                <TableHead>Signal</TableHead>
                <TableHead>Channel</TableHead>
                <TableHead>Location</TableHead>
                <TableHead>Flag</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(series ?? []).slice(-40).reverse().map((p, i) => (
                <TableRow key={`${p.at}-${i}`}>
                  <TableCell className="text-xs">{formatDateTime(p.at)}</TableCell>
                  <TableCell className="text-xs">{p.signalDbm} dBm</TableCell>
                  <TableCell className="text-xs">{p.channel}</TableCell>
                  <TableCell className="text-xs">
                    {p.lat != null && p.lon != null ? `${p.lat.toFixed(5)}, ${p.lon.toFixed(5)}` : '—'}
                  </TableCell>
                  <TableCell><StatusBadge value={p.locationFlag} /></TableCell>
                </TableRow>
              ))}
              {(series ?? []).length === 0 ? (
                <TableRow><TableCell colSpan={5} className="text-center text-sm text-foreground/60">No detections recorded.</TableCell></TableRow>
              ) : null}
            </TableBody>
          </Table>
          <div className="px-4 py-3 text-xs text-foreground/60">
            <Badge variant="neutral" className="mr-2">gps</Badge> resolved from device GPS ·{' '}
            <Badge variant="neutral" className="mx-2">inferred</Badge> interpolated from swarm ·{' '}
            <Badge variant="neutral" className="mx-2">unlocated</Badge> no GPS evidence
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
