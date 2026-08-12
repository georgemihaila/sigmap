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
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { useIsOperator } from '@/hooks/useUser';
import { timeAgo } from '@/lib/time';

export function PairingPage() {
  const { data: sessions } = useListSessionsQuery();
  const { data: pending, isLoading, isError } = useListPendingPairingsQuery();
  const [approve] = useApprovePairingMutation();
  const [reject] = useRejectPairingMutation();
  const isOperator = useIsOperator();

  const [qrSession, setQrSession] = useState<string>('');
  const { data: qr } = useGetPairingQrQuery(qrSession, { skip: !qrSession });
  const [approvalSessions, setApprovalSessions] = useState<Record<string, string>>({});

  const active = sessions?.filter((s) => s.status === 'active') ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Pairing"
        description="Pair new scanner devices with a QR code and approve join requests."
      />

      {isError ? (
        <Alert variant="destructive">
          <AlertTitle>Failed to load pairing requests</AlertTitle>
          <AlertDescription>Check the connection and retry.</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>QR pairing</CardTitle>
            <CardDescription>Generate a short-lived pairing token for a session.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col items-center gap-4">
            <div className="flex w-full flex-col gap-2">
              <Label className="text-xs">Session</Label>
              <Select value={qrSession} onValueChange={setQrSession}>
                <SelectTrigger className="bg-background text-foreground"><SelectValue placeholder="Select session" /></SelectTrigger>
                <SelectContent>
                  {sessions?.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>

            {qr ? (
              <>
                <div className="rounded-base border-2 border-border bg-white p-4 shadow-shadow">
                  <QRCodeSVG value={qr.payload} size={180} />
                </div>
                <div className="flex w-full flex-col gap-2">
                  <div className="flex items-center justify-between rounded-base border-2 border-border bg-secondary-background px-3 py-2 text-sm">
                    <code className="truncate text-xs">{qr.payload}</code>
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
                  </div>
                  <p className="text-center text-xs text-foreground/60">Token {qr.token} — scan with the Android app or a scanner agent.</p>
                </div>
              </>
            ) : (
              <p className="text-sm text-foreground/60">Pick a session to generate a QR payload.</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Pending approvals</CardTitle>
            <CardDescription>Devices waiting for an operator to admit them to a session.</CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow className="bg-secondary-background">
                  <TableHead>Device</TableHead>
                  <TableHead>Capabilities</TableHead>
                  <TableHead>Requested</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  <TableRow><TableCell colSpan={4}><Skeleton className="h-10 w-full" /></TableCell></TableRow>
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
                    <TableCell className="text-right">
                      {isOperator ? (
                        <div className="flex flex-col items-end gap-1.5">
                          <Select
                            value={approvalSessions[p.id] ?? ''}
                            onValueChange={(v: string) => setApprovalSessions((prev) => ({ ...prev, [p.id]: v }))}
                          >
                            <SelectTrigger className="h-8 w-40 bg-background text-foreground">
                              <SelectValue placeholder={active[0]?.name ?? 'Session…'} />
                            </SelectTrigger>
                            <SelectContent>
                              {active.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
                            </SelectContent>
                          </Select>
                          <div className="flex items-center gap-1.5">
                            <Button
                              variant="noShadow"
                              size="sm"
                              onClick={async () => {
                                const target = approvalSessions[p.id] ?? active[0]?.id;
                                if (!target) { toast.error('Pick a session to admit into'); return; }
                                await approve({ deviceId: p.deviceId, sessionId: target }).unwrap();
                                toast.success(`${p.deviceName} admitted`);
                              }}
                            >
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
                        </div>
                      ) : (
                        <StatusBadge value={p.status} />
                      )}
                    </TableCell>
                  </TableRow>
                ))}
                {pending?.length === 0 && !isLoading ? (
                  <TableRow><TableCell colSpan={4} className="text-center text-sm text-foreground/60">No pending pairing requests.</TableCell></TableRow>
                ) : null}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
