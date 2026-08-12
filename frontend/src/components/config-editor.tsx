import { Plus, Trash2 } from 'lucide-react';
import type { InterfaceConfig, ScanConfig } from '@/lib/domain';
import { defaultScanConfig } from '@/lib/scanConfig';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

function InterfaceEditor({
  value,
  onChange,
}: {
  value: InterfaceConfig;
  onChange: (next: InterfaceConfig) => void;
}) {
  return (
    <div className="rounded-base border-2 border-border bg-secondary-background p-3">
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">Interface</Label>
          <Input value={value.name} onChange={(e) => onChange({ ...value, name: e.target.value })} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">Driver</Label>
          <Input value={value.driver} onChange={(e) => onChange({ ...value, driver: e.target.value })} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">Channels (comma-separated, empty = all)</Label>
          <Input
            value={value.channels.join(',')}
            placeholder="1,6,11"
            onChange={(e) =>
              onChange({
                ...value,
                channels: e.target.value
                  .split(',')
                  .map((c) => Number(c.trim()))
                  .filter((n) => !Number.isNaN(n)),
              })
            }
          />
        </div>
        <div className="flex items-end gap-4">
          <div className="flex items-center gap-2">
            <Switch aria-label={`${value.name} enabled`} checked={value.enabled} onCheckedChange={(v: boolean) => onChange({ ...value, enabled: v })} />
            <Label className="text-xs">Enabled</Label>
          </div>
          <div className="flex items-center gap-2">
            <Switch aria-label={`${value.name} monitor mode`} checked={value.monitorMode} onCheckedChange={(v: boolean) => onChange({ ...value, monitorMode: v })} />
            <Label className="text-xs">Monitor mode</Label>
          </div>
        </div>
      </div>
    </div>
  );
}

export function ConfigEditor({
  value,
  onChange,
}: {
  value: ScanConfig;
  onChange: (next: ScanConfig) => void;
}) {
  const update = (patch: Partial<ScanConfig>) => onChange({ ...value, ...patch });
  const updateInterface = (index: number, next: InterfaceConfig) => {
    const interfaces = value.interfaces.map((i, idx) => (idx === index ? next : i));
    onChange({ ...value, interfaces });
  };
  const removeInterface = (index: number) => {
    onChange({ ...value, interfaces: value.interfaces.filter((_, idx) => idx !== index) });
  };
  const addInterface = () => {
    onChange({
      ...value,
      interfaces: [...value.interfaces, { name: 'wlanX', driver: 'nl80211', monitorMode: true, enabled: true, channels: [] }],
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-3">
        {(
          [
            ['scanWifi', 'Scan WiFi'],
            ['scanBluetooth', 'Scan Bluetooth'],
            ['scanBtLe', 'Scan BT LE'],
            ['scanClientsPromiscuous', 'Client capture'],
          ] as Array<[keyof ScanConfig, string]>
        ).map(([key, label]) => (
          <div key={key} className="flex items-center justify-between rounded-base border-2 border-border bg-secondary-background p-3">
            <Label className="text-sm">{label}</Label>
            <Switch
              aria-label={label}
              checked={Boolean(value[key])}
              onCheckedChange={(v: boolean) => update({ [key]: v } as Partial<ScanConfig>)}
            />
          </div>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">Channel hop (ms)</Label>
          <Input
            type="number"
            value={value.channelHopMs}
            onChange={(e) => update({ channelHopMs: Number(e.target.value) || 0 })}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">Batch interval (ms)</Label>
          <Input
            type="number"
            value={value.batchIntervalMs}
            onChange={(e) => update({ batchIntervalMs: Number(e.target.value) || 0 })}
          />
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <div className="flex items-center justify-between">
          <Label className="text-sm font-heading">Interfaces</Label>
          <Button type="button" variant="noShadow" size="sm" onClick={addInterface}>
            <Plus /> Add interface
          </Button>
        </div>
        {value.interfaces.map((iface, idx) => (
          <div key={idx} className="flex items-start gap-2">
            <div className="flex-1">
              <InterfaceEditor value={iface} onChange={(next) => updateInterface(idx, next)} />
            </div>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  type="button"
                  variant="neutral"
                  size="icon"
                  className="mt-0 size-8"
                  aria-label="Remove interface"
                  onClick={() => removeInterface(idx)}
                >
                  <Trash2 className="size-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>Remove interface</TooltipContent>
            </Tooltip>
          </div>
        ))}
        {value.interfaces.length === 0 ? (
          <p className="text-xs text-foreground/60">No interfaces — all radios effectively off.</p>
        ) : null}
      </div>

      <Button
        type="button"
        variant="neutral"
        size="sm"
        className="self-start"
        onClick={() => onChange(defaultScanConfig())}
      >
        Reset to default
      </Button>
    </div>
  );
}
