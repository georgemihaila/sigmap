import { useState } from 'react';
import { CheckCircle2, Pencil, Play, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { useListPresetsQuery, useCreatePresetMutation, useUpdatePresetMutation, useDeletePresetMutation, usePreviewPresetOnFleetQuery, useApplyPresetToDevicesMutation } from '@/api/presetsApi';
import { useListSessionsQuery } from '@/api/sessionsApi';
import { PageHeader } from '@/components/page-header';
import { ConfigEditor } from '@/components/config-editor';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Skeleton } from '@/components/ui/skeleton';
import { useIsOperator } from '@/hooks/useUser';
import { parseScanConfig } from '@/lib/scanConfig';
import type { ConfigDiff, ScanPreset } from '@/lib/domain';

function PresetForm({
  preset,
  onSave,
  onCancel,
  saving,
}: {
  preset: ScanPreset | null;
  onSave: (input: { name: string; description: string; configJson: string }) => void;
  onCancel: () => void;
  saving: boolean;
}) {
  const [name, setName] = useState(preset?.name ?? '');
  const [description, setDescription] = useState(preset?.description ?? '');
  const [config, setConfig] = useState(() => parseScanConfig(preset?.configJson));

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave({ name: name.trim() || 'Untitled preset', description: description.trim(), configJson: JSON.stringify(config) });
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-4">
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        <div className="flex flex-col gap-2">
          <Label htmlFor="preset-name">Name</Label>
          <Input id="preset-name" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="preset-desc">Description</Label>
          <Textarea id="preset-desc" value={description} onChange={(e) => setDescription(e.target.value)} rows={1} />
        </div>
      </div>
      <ConfigEditor value={config} onChange={setConfig} />
      <DialogFooter>
        <Button type="button" variant="neutral" onClick={onCancel} disabled={saving}>Cancel</Button>
        <Button type="submit" disabled={saving}>{preset ? 'Save changes' : 'Create preset'}</Button>
      </DialogFooter>
    </form>
  );
}

export function PresetsPage() {
  const { data: presets, isLoading } = useListPresetsQuery();
  const { data: sessions } = useListSessionsQuery();
  const [createPreset] = useCreatePresetMutation();
  const [updatePreset] = useUpdatePresetMutation();
  const [deletePreset] = useDeletePresetMutation();
  const [applyPreset] = useApplyPresetToDevicesMutation();
  const isOperator = useIsOperator();

  const [editor, setEditor] = useState<{ mode: 'create' } | { mode: 'edit'; preset: ScanPreset } | null>(null);
  const [applying, setApplying] = useState<ScanPreset | null>(null);
  const [applySession, setApplySession] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [saving, setSaving] = useState(false);

  const { data: preview, isFetching: previewing } = usePreviewPresetOnFleetQuery(
    { presetId: applying?.id ?? '', sessionId: applySession ?? '' },
    { skip: !applying || !applySession },
  );

  const save = async (input: { name: string; description: string; configJson: string }) => {
    setSaving(true);
    try {
      if (editor?.mode === 'create') {
        await createPreset(input).unwrap();
        toast.success('Preset created');
      } else if (editor?.mode === 'edit') {
        await updatePreset({ id: editor.preset.id, body: input }).unwrap();
        toast.success('Preset updated');
      }
      setEditor(null);
    } finally {
      setSaving(false);
    }
  };

  const openApply = (preset: ScanPreset) => {
    const defaultSession = sessions?.find((s) => s.status === 'active')?.id ?? sessions?.[0]?.id ?? null;
    setApplying(preset);
    setApplySession(defaultSession);
    setSelected(new Set());
  };

  const confirmApply = async () => {
    if (!applying || !applySession) return;
    const deviceIds = selected.size > 0 ? [...selected] : preview?.map((d) => d.deviceId) ?? [];
    try {
      const results = await applyPreset({ sessionId: applySession, presetId: applying.id, deviceIds }).unwrap();
      toast.success(`Pushed "${applying.name}" to ${results.length} device(s)`);
      setApplying(null);
    } catch {
      toast.error('Apply failed');
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Presets"
        description="Reusable scan configurations, global across sessions."
        actions={
          isOperator ? (
            <Button onClick={() => setEditor({ mode: 'create' })}><Plus /> New preset</Button>
          ) : null
        }
      />

      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-44" />)}
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
        {presets?.map((p) => {
          const cfg = parseScanConfig(p.configJson);
          return (
            <Card key={p.id} className="gap-3 py-5">
              <CardHeader className="px-5">
                <div className="flex items-center justify-between gap-2">
                  <CardTitle>{p.name}</CardTitle>
                  {p.isBuiltin ? <Badge variant="neutral">built-in</Badge> : null}
                </div>
                <CardDescription>{p.description ?? '—'}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-3 px-5">
                <div className="flex flex-wrap gap-1.5">
                  <Badge variant="neutral">{cfg.interfaces.length} iface(s)</Badge>
                  <Badge variant="neutral">{cfg.channelHopMs}ms hop</Badge>
                  {cfg.scanWifi ? <Badge className="bg-emerald-400 text-emerald-950">WiFi</Badge> : null}
                  {cfg.scanBluetooth ? <Badge className="bg-emerald-400 text-emerald-950">BT</Badge> : null}
                  {cfg.scanBtLe ? <Badge className="bg-emerald-400 text-emerald-950">BLE</Badge> : null}
                  {cfg.scanClientsPromiscuous ? <Badge className="bg-main text-main-foreground">clients</Badge> : null}
                </div>
                {isOperator ? (
                  <div className="mt-1 flex items-center gap-1.5">
                    <Button variant="noShadow" size="sm" onClick={() => openApply(p)} disabled={p.isBuiltin}>
                      <Play /> Apply…
                    </Button>
                    <Button variant="neutral" size="sm" onClick={() => setEditor({ mode: 'edit', preset: p })} disabled={p.isBuiltin}>
                      <Pencil /> Edit
                    </Button>
                    {!p.isBuiltin ? (
                      <Button
                        variant="neutral"
                        size="sm"
                        className="ml-auto"
                        aria-label={`Delete ${p.name}`}
                        onClick={async () => {
                          await deletePreset(p.id);
                          toast.success(`Deleted "${p.name}"`);
                        }}
                      >
                        <Trash2 />
                      </Button>
                    ) : null}
                  </div>
                ) : null}
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Dialog open={editor !== null} onOpenChange={(open: boolean) => !open && setEditor(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editor?.mode === 'edit' ? 'Edit preset' : 'New preset'}</DialogTitle>
          </DialogHeader>
          {editor ? (
            <PresetForm
              key={editor.mode === 'edit' ? editor.preset.id : 'new'}
              preset={editor.mode === 'edit' ? editor.preset : null}
              onSave={save}
              onCancel={() => setEditor(null)}
              saving={saving}
            />
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog open={applying !== null} onOpenChange={(open: boolean) => !open && setApplying(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Apply "{applying?.name}"</DialogTitle>
            <DialogDescription>
              Choose a session to preview the diff against each device's live config before pushing.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label>Session</Label>
              <Select value={applySession ?? ''} onValueChange={(v: string) => { setApplySession(v); setSelected(new Set()); }}>
                <SelectTrigger className="bg-background text-foreground"><SelectValue placeholder="Select session" /></SelectTrigger>
                <SelectContent>
                  {sessions?.map((s) => (
                    <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {previewing ? <Skeleton className="h-24 w-full" /> : null}

            {preview && !previewing ? (
              <Accordion type="multiple" className="flex flex-col gap-2">
                {preview.map((diff: ConfigDiff) => (
                  <AccordionItem key={diff.deviceId} value={diff.deviceId} className="border-2 border-border rounded-base bg-secondary-background px-3">
                    <AccordionTrigger className="py-3">
                      <span className="flex items-center gap-2">
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
                          onClick={(e: React.MouseEvent) => e.stopPropagation()}
                          aria-label={`Select ${diff.deviceName}`}
                        />
                        <span className="font-base">{diff.deviceName}</span>
                        {diff.equal ? (
                          <Badge className="bg-emerald-400 text-emerald-950"><CheckCircle2 className="size-3" /> in sync</Badge>
                        ) : (
                          <Badge className="bg-amber-400 text-amber-950">{diff.changes.length} change(s)</Badge>
                        )}
                      </span>
                    </AccordionTrigger>
                    <AccordionContent>
                      {diff.equal ? (
                        <p className="text-sm text-foreground/60">Config already matches — no changes.</p>
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
            ) : null}

            {applySession && preview?.length === 0 ? (
              <p className="text-sm text-foreground/60">No devices are assigned to this session.</p>
            ) : null}
          </div>
          <DialogFooter>
            <Button variant="neutral" onClick={() => setApplying(null)}>Cancel</Button>
            <Button onClick={confirmApply} disabled={!applySession}>
              <Play /> Push to {selected.size > 0 ? selected.size : preview?.length ?? 0} device(s)
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
