import { useState } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { Check, Copy, X } from 'lucide-react';
import { toast } from 'sonner';
import { useListSessionsQuery } from '@/api/sessionsApi';
import { useListPendingPairingsQuery, useApprovePairingMutation, useRejectPairingMutation, useGetPairingQrQuery } from '@/api/pairingApi';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { useIsOperator } from '@/hooks/useUser';
import { timeAgo } from '@/lib/time';
import type { PairingRequest } from '@/lib/domain';

export function PairingPage() {
  const { data: sessions } = useListSessionsQuery();
  const { data: pending, isLoading, isError } = useListPendingPairingsQuery();
  const [approve] = useApprovePairingMutation();
  const [reject] = useRejectPairingMutation();
  const isOperator = useIsOperator();

  const { data: qr } = useGetPairingQrQuery();
  const [approvalSessions, setApprovalSessions] = useState<Record<string, string>>({});

  const assignable = sessions?.filter((s) => s.status !== 'archived') ?? [];

  const approveOne = async (p: PairingRequest) => {
    const picked = approvalSessions[p.id];
    const sessionId = picked && picked !== 'none' ? picked : null;
    try {
      await approve({ deviceId: p.deviceId, sessionId }).unwrap();
      toast.success(sessionId ? `${p.deviceName} paired and assigned` : `${p.deviceName} paired`);
    } catch {
      toast.error(`Failed to pair ${p.deviceName}`);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Pairing"
        description="Pair scanner devices with this platform using a QR code, then assign them to sessions."
      />

      {isError ? (
        <Alert variant="destructive">
          <AlertTitle>Failed to load pairing requests</AlertTitle>
          <AlertDescription>Check the connection and retry.</AlertDescription>
        </Alert>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Pair a scanner</CardTitle>
          <CardDescription>
            Scan this QR from the Sigmap app. Pairing is permanent — a device joins the fleet and can be
            assigned to a session at any time.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col items-center gap-4">
          {qr ? (
            <>
              <div className="rounded-base border-2 border-border bg-white p-4 shadow-shadow">
                <QRCodeSVG value={qr.payload} size={220} />
              </div>
              <div className="flex w-full max-w-sm flex-col gap-2">
                <div className="flex items-center justify-between rounded-base border-2 border-border bg-secondary-background px-3 py-2 text-sm">
                  <code className="truncate text-xs">{qr.payload}</code>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Button
                        variant="noShadow"
                        size="icon"
                        className="size-7"
                        aria-label="Copy pairing payload"
                        onClick={() => {
                          navigator.clipboard?.writeText(qr.payload);
                          toast.success('Payload copied');
                        }}
                      >
                        <Copy className="size-3.5" />
                      </Button>
                    </TooltipTrigger>
                    <TooltipContent>Copy pairing payload</TooltipContent>
                  </Tooltip>
                </div>
                <p className="text-center text-xs text-foreground/60">
                  Pairing code <code className="rounded-base border-2 border-border bg-secondary-background px-1">{qr.token}</code> — scan
                  it to request admission to the fleet.
                </p>
              </div>
            </>
          ) : (
            <Skeleton className="h-56 w-56 rounded-base" />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Pending approvals</CardTitle>
          <CardDescription>Scanners that scanned the pairing code and are waiting to be admitted to the fleet.</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow className="bg-secondary-background">
                <TableHead>Device</TableHead>
                <TableHead>Capabilities</TableHead>
                <TableHead>Requested</TableHead>
                <TableHead>Assign to session</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow><TableCell colSpan={5}><Skeleton className="h-10 w-full" /></TableCell></TableRow>
              ) : null}
              {pending?.map((p) => (
                <TableRow key={p.id}>
                  <TableCell>
                    <div className="font-heading">{p.deviceName}</div>
                    <div className="text-xs text-foreground/60">{p.platform}</div>
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1">
                      {p.capabilities.hasWifiMonitor ? <Badge variant="neutral">WiFi</Badge> : null}
                      {p.capabilities.hasBluetooth ? <Badge variant="neutral">BT</Badge> : null}
                      {p.capabilities.hasGps ? <Badge variant="neutral">GPS</Badge> : null}
                      {p.capabilities.hasBattery ? <Badge variant="neutral">battery</Badge> : null}
                    </div>
                  </TableCell>
                  <TableCell className="text-xs text-foreground/60">{timeAgo(p.requestedAt)}</TableCell>
                  <TableCell>
                    {isOperator ? (
                      <Select
                        value={approvalSessions[p.id] ?? 'none'}
                        onValueChange={(v: string) => setApprovalSessions((prev) => ({ ...prev, [p.id]: v }))}
                      >
                        <SelectTrigger className="h-8 w-44 bg-background text-foreground">
                          <SelectValue placeholder="No session" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">No session</SelectItem>
                          {assignable.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    ) : (
                      <span className="text-sm text-foreground/60">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-right">
                    {isOperator ? (
                      <div className="flex items-center justify-end gap-1.5">
                        <Button variant="noShadow" size="sm" onClick={() => approveOne(p)}>
                          <Check /> Approve
                        </Button>
                        <Button
                          variant="neutral"
                          size="sm"
                          onClick={async () => {
                            await reject(p.deviceId).unwrap();
                            toast.success(`${p.deviceName} rejected`);
                          }}
                        >
                          <X /> Reject
                        </Button>
                      </div>
                    ) : (
                      <StatusBadge value={p.status} />
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {pending?.length === 0 && !isLoading ? (
                <TableRow><TableCell colSpan={5} className="text-center text-sm text-foreground/60">No pending pairing requests.</TableCell></TableRow>
              ) : null}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
