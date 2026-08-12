import { useState } from 'react';
import { Download, FileUp, RefreshCw, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { useListSessionsQuery } from '@/api/sessionsApi';
import { useListExportsQuery, useCreateExportMutation, useLazyDownloadExportQuery, useUploadWigleMutation, useImportWigleMutation, useGetWigleSettingsQuery, usePutWigleSettingsMutation, type ExportInput } from '@/api/exportsApi';
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/status-badge';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatDateTime, formatNumber } from '@/lib/time';
import type { ExportFormat } from '@/lib/domain';

const FORMATS: Array<{ value: ExportFormat; label: string; hint: string }> = [
  { value: 'wigle_csv', label: 'WiGLE CSV', hint: 'Directly uploadable to WiGLE.net' },
  { value: 'csv', label: 'Generic CSV', hint: 'Plain CSV of detections' },
  { value: 'geojson', label: 'GeoJSON', hint: 'Locations as GeoJSON' },
];

export function ExportsPage() {
  const { data: sessions } = useListSessionsQuery();
  const { data: exports, isLoading } = useListExportsQuery(undefined, { pollingInterval: 3000 });
  const [createExport] = useCreateExportMutation();
  const [download] = useLazyDownloadExportQuery();
  const [uploadWigle] = useUploadWigleMutation();
  const [importWigle] = useImportWigleMutation();
  const { data: wigleSettings } = useGetWigleSettingsQuery();
  const [putWigle] = usePutWigleSettingsMutation();

  const [sessionId, setSessionId] = useState<string>('all');
  const [format, setFormat] = useState<ExportFormat>('wigle_csv');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [wigleSession, setWigleSession] = useState<string>('');
  const [apiName, setApiName] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  const runExport = async () => {
    const input: ExportInput = {
      sessionId: sessionId === 'all' ? null : sessionId,
      format,
      from: from || null,
      to: to || null,
    };
    try {
      await createExport(input).unwrap();
      toast.success('Export queued');
    } catch {
      toast.error('Failed to queue export');
    }
  };

  const runDownload = async (id: string) => {
    try {
      const text = await download(id).unwrap();
      const blob = new Blob([text], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `sigmap-export-${id.slice(0, 8)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      toast.error('Export is not ready yet');
    }
  };

  const saveWigle = async () => {
    await putWigle({
      ...(apiName ? { apiName } : {}),
      ...(apiKey ? { apiKey } : {}),
      ...(username ? { username } : {}),
      ...(password ? { password } : {}),
    }).unwrap();
    setApiKey('');
    setPassword('');
    toast.success('WiGLE settings saved');
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Export Center" description="WiGLE, CSV and GeoJSON exports, plus WiGLE.net integration." />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>Create export</CardTitle>
            <CardDescription>Scope by session and format.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label className="text-xs">Session</Label>
              <Select value={sessionId} onValueChange={setSessionId}>
                <SelectTrigger className="bg-background text-foreground"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All sessions</SelectItem>
                  {sessions?.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-2">
              <Label className="text-xs">Format</Label>
              <Select value={format} onValueChange={(v: string) => setFormat(v as ExportFormat)}>
                <SelectTrigger className="bg-background text-foreground"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {FORMATS.map((f) => <SelectItem key={f.value} value={f.value}>{f.label}</SelectItem>)}
                </SelectContent>
              </Select>
              <p className="text-xs text-foreground/60">{FORMATS.find((f) => f.value === format)?.hint}</p>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-2">
                <Label className="text-xs">From</Label>
                <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
              </div>
              <div className="flex flex-col gap-2">
                <Label className="text-xs">To</Label>
                <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
              </div>
            </div>
            <Button onClick={runExport}><Download /> Queue export</Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>WiGLE.net</CardTitle>
            <CardDescription>Upload session observations or import matches from WiGLE.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label className="text-xs">Session to upload</Label>
              <Select value={wigleSession} onValueChange={setWigleSession}>
                <SelectTrigger className="bg-background text-foreground"><SelectValue placeholder="Select session" /></SelectTrigger>
                <SelectContent>
                  {sessions?.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center gap-2">
              <Button variant="noShadow" onClick={async () => {
                if (!wigleSession) { toast.error('Pick a session first'); return; }
                await uploadWigle({ sessionId: wigleSession }).unwrap();
                toast.success('Upload queued');
              }}><Upload /> Upload session</Button>
              <Button variant="neutral" onClick={async () => {
                const res = await importWigle().unwrap();
                toast.success(`Imported ${res.imported} observations`);
              }}><FileUp /> Import matches</Button>
            </div>
            <div className="flex flex-col gap-2 border-t-2 border-border pt-4">
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1.5">
                  <Label className="text-xs">API name</Label>
                  <Input value={apiName} onChange={(e) => setApiName(e.target.value)} placeholder={wigleSettings?.apiName ?? 'wigle.net'} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label className="text-xs">API key</Label>
                  <Input type="password" value={apiKey} onChange={(e) => setApiKey(e.target.value)} placeholder={wigleSettings?.apiKeySet ? '••••••••' : ''} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label className="text-xs">Username</Label>
                  <Input value={username} onChange={(e) => setUsername(e.target.value)} placeholder={wigleSettings?.username ?? ''} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label className="text-xs">Password</Label>
                  <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder={wigleSettings?.passwordSet ? '••••••••' : ''} />
                </div>
              </div>
              <Button variant="neutral" className="self-start" onClick={saveWigle}>Save credentials</Button>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recent exports</CardTitle>
            <CardDescription>Jobs process asynchronously; refresh automatically.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {isLoading ? <p className="text-sm text-foreground/60">Loading…</p> : null}
            {exports?.slice(0, 10).map((e) => (
              <div key={e.id} className="flex items-center justify-between gap-2 rounded-base border-2 border-border bg-secondary-background px-3 py-2">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 text-sm">
                    <Badge variant="neutral">{e.format.replace('_', ' ')}</Badge>
                    <span className="truncate font-base">{e.sessionName ?? 'All sessions'}</span>
                  </div>
                  <div className="mt-0.5 text-xs text-foreground/60">
                    {formatDateTime(e.createdAt)} · {e.rowCount != null ? `${formatNumber(e.rowCount)} rows` : ''}
                  </div>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <StatusBadge value={e.status} />
                  {e.status === 'done' ? (
                    <Button variant="noShadow" size="icon" className="size-8" aria-label="Download" onClick={() => runDownload(e.id)}>
                      <Download className="size-4" />
                    </Button>
                  ) : e.status === 'running' || e.status === 'queued' ? (
                    <RefreshCw className="size-4 animate-spin text-foreground/50" />
                  ) : null}
                </div>
              </div>
            ))}
            {exports?.length === 0 ? <p className="text-sm text-foreground/60">No exports yet.</p> : null}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>All exports</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow className="bg-secondary-background">
                <TableHead>Format</TableHead>
                <TableHead>Session</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Rows</TableHead>
                <TableHead>Created</TableHead>
                <TableHead className="text-right">Download</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {exports?.map((e) => (
                <TableRow key={e.id}>
                  <TableCell><Badge variant="neutral">{e.format.replace('_', ' ')}</Badge></TableCell>
                  <TableCell className="font-base">{e.sessionName ?? 'All sessions'}</TableCell>
                  <TableCell><StatusBadge value={e.status} /></TableCell>
                  <TableCell>{e.rowCount != null ? formatNumber(e.rowCount) : '—'}</TableCell>
                  <TableCell className="text-xs">{formatDateTime(e.createdAt)}</TableCell>
                  <TableCell className="text-right">
                    {e.status === 'done' ? (
                      <Button variant="noShadow" size="sm" onClick={() => runDownload(e.id)}><Download /> CSV</Button>
                    ) : (
                      <span className="text-xs text-foreground/50">—</span>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {exports?.length === 0 ? (
                <TableRow><TableCell colSpan={6} className="text-center text-sm text-foreground/60">No exports yet.</TableCell></TableRow>
              ) : null}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
