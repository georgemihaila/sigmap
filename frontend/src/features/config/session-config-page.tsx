import { useState } from 'react';
import { CheckCircle2, GitCompareArrows, Send } from 'lucide-react';
import { toast } from 'sonner';
import { useListSessionFleetQuery } from '@/api/fleetApi';
import { useListPresetsQuery } from '@/api/presetsApi';
import { usePreviewSessionConfigMutation, useApplySessionConfigMutation } from '@/api/configApi';
import { useSessionId } from '@/features/sessions/session-context';
import { ConfigEditor } from '@/components/config-editor';
import { PageHeader } from '@/components/page-header';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { useIsOperator } from '@/hooks/useUser';
import { defaultScanConfig, parseScanConfig, stringifyScanConfig } from '@/lib/scanConfig';
import type { ConfigDiff } from '@/lib/domain';

export function SessionConfigPage() {
  const sessionId = useSessionId() ?? '';
  const isOperator = useIsOperator();
  const { data: fleet, isLoading: fleetLoading, isError: fleetError } = useListSessionFleetQuery(sessionId, { skip: !sessionId });
  const { data: presets } = useListPresetsQuery();

  const [target, setTarget] = useState(() => defaultScanConfig());
  const [diffs, setDiffs] = useState<ConfigDiff[] | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const [preview, { isLoading: previewing }] = usePreviewSessionConfigMutation();
  const [push, { isLoading: pushing }] = useApplySessionConfigMutation();

  const devices = fleet ?? [];
  const changedCount = diffs?.filter((d) => !d.equal).length ?? 0;

  const loadFromPreset = (presetId: string) => {
    const preset = presets?.find((p) => p.id === presetId);
    if (!preset) return;
    setTarget(parseScanConfig(preset.configJson));
    setDiffs(null);
  };

  const loadFromDevice = (deviceId: string) => {
    const f = fleet?.find((d) => d.device.id === deviceId);
    if (!f) return;
    setTarget(parseScanConfig(f.config?.configJson));
    setDiffs(null);
  };

  const runPreview = async () => {
    try {
      const result = await preview({ sessionId, configJson: stringifyScanConfig(target) }).unwrap();
      setDiffs(result);
      setSelected(new Set(result.map((d) => d.deviceId)));
    } catch {
      toast.error('Preview failed');
    }
  };

  const runPush = async () => {
    const deviceIds = selected.size > 0 ? [...selected] : diffs?.map((d) => d.deviceId) ?? [];
    try {
      const results = await push({ sessionId, configJson: stringifyScanConfig(target), deviceIds }).unwrap();
      toast.success(`Config pushed to ${results.length} device(s)`);
    } catch {
      toast.error('Push failed');
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Bulk config"
        description="Edit a target scan config and preview the per-device diff before pushing."
      />

      {fleetError ? (
        <Alert variant="destructive">
          <AlertTitle>Failed to load session fleet</AlertTitle>
          <AlertDescription>Check the connection and retry.</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Target config</CardTitle>
            <CardDescription>Start from a preset or a device's current config, then tweak.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-2">
                <Label className="text-xs">Load from preset</Label>
                <Select onValueChange={(v: string) => loadFromPreset(v)}>
                  <SelectTrigger className="bg-background text-foreground"><SelectValue placeholder="Pick a preset…" /></SelectTrigger>
                  <SelectContent>
                    {presets?.map((p) => (
                      <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="flex flex-col gap-2">
                <Label className="text-xs">Start from device</Label>
                <Select onValueChange={(v: string) => loadFromDevice(v)}>
                  <SelectTrigger className="bg-background text-foreground"><SelectValue placeholder="Pick a device…" /></SelectTrigger>
                  <SelectContent>
                    {fleet?.map((f) => (
                      <SelectItem key={f.device.id} value={f.device.id}>{f.device.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            {fleetLoading ? <Skeleton className="h-40 w-full" /> : <ConfigEditor value={target} onChange={(c) => { setTarget(c); setDiffs(null); }} />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex-row items-center justify-between">
            <CardTitle className="flex items-center gap-2"><GitCompareArrows className="size-4" /> Diff preview</CardTitle>
            <Button onClick={runPreview} disabled={previewing}>
              {previewing ? 'Diffing…' : 'Preview diff'}
            </Button>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {!diffs ? (
              <p className="text-sm text-foreground/60">
                Run a preview to see exactly which devices change and how. Nothing is pushed until you confirm.
              </p>
            ) : (
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="neutral">{devices.length} device(s)</Badge>
                  <Badge className="bg-amber-400 text-amber-950">{changedCount} will change</Badge>
                  <Badge className="bg-emerald-400 text-emerald-950">{devices.length - changedCount} in sync</Badge>
                </div>
                <Accordion type="multiple" className="flex flex-col gap-2">
                  {diffs.map((diff) => (
                    <AccordionItem key={diff.deviceId} value={diff.deviceId} className="rounded-base border-2 border-border bg-secondary-background px-3">
                      <div className="flex items-center gap-2">
                        <Checkbox
                          checked={selected.has(diff.deviceId)}
                          onCheckedChange={(v: boolean) => {
                            setSelected((prev) => {
                              const next = new Set(prev);
                              if (v) next.add(diff.deviceId);
                              else next.delete(diff.deviceId);
                              return next;
                            });
                          }}
                          aria-label={`Include ${diff.deviceName}`}
                        />
                        <AccordionTrigger className="flex-1 py-3">
                          <span className="flex items-center gap-2">
                            <span className="font-base">{diff.deviceName}</span>
                            {diff.equal ? (
                              <Badge className="bg-emerald-400 text-emerald-950"><CheckCircle2 className="size-3" /> in sync</Badge>
                            ) : (
                              <Badge className="bg-amber-400 text-amber-950">{diff.changes.length} change(s)</Badge>
                            )}
                          </span>
                        </AccordionTrigger>
                      </div>
                      <AccordionContent>
                        {diff.equal ? (
                          <p className="text-sm text-foreground/60">No changes for this device.</p>
                        ) : (
                          <ul className="flex flex-col gap-1.5">
                            {diff.changes.map((c, i) => (
                              <li key={i} className="flex flex-wrap items-center gap-2 text-sm">
                                <code className="rounded-base border-2 border-border bg-background px-1.5 py-0.5">{c.path}</code>
                                <span className="text-foreground/50 line-through">{c.before}</span>
                                <span aria-hidden>→</span>
                                <span className="font-base">{c.after}</span>
                              </li>
                            ))}
                          </ul>
                        )}
                      </AccordionContent>
                    </AccordionItem>
                  ))}
                </Accordion>
                {isOperator ? (
                  <Button onClick={runPush} disabled={pushing || changedCount === 0} className="self-end">
                    <Send /> Push to {selected.size} device(s)
                  </Button>
                ) : (
                  <p className="text-xs text-foreground/60">Operator role required to push config.</p>
                )}
              </>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
