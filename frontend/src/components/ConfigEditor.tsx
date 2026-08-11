import { Button, NumberInput, Select, Stack, Switch, TextInput } from '@mantine/core';
import { useState } from 'react';
import { InterfaceConfig, ScanConfig } from '../generated/config_pb';

export interface ConfigEditorState {
  scanWifi: boolean;
  scanBluetooth: boolean;
  scanBtLe: boolean;
  scanClientsPromiscuous: boolean;
  channelHopMs: number;
  batchIntervalMs: number;
  interfaces: string[];
}

export function toState(config: ScanConfig): ConfigEditorState {
  return {
    scanWifi: config.scanWifi,
    scanBluetooth: config.scanBluetooth,
    scanBtLe: config.scanBtLe,
    scanClientsPromiscuous: config.scanClientsPromiscuous,
    channelHopMs: config.channelHopMs,
    batchIntervalMs: config.batchIntervalMs,
    interfaces: (config.interfaces ?? []).map((i) => i.name),
  };
}

export function stateToConfig(state: ConfigEditorState): ScanConfig {
  return new ScanConfig({
    scanWifi: state.scanWifi,
    scanBluetooth: state.scanBluetooth,
    scanBtLe: state.scanBtLe,
    scanClientsPromiscuous: state.scanClientsPromiscuous,
    channelHopMs: state.channelHopMs,
    batchIntervalMs: state.batchIntervalMs,
    interfaces: state.interfaces.map((name) => new InterfaceConfig({ name, monitorMode: true, enabled: true })),
  });
}

interface Props {
  initial: ConfigEditorState;
  onChange: (state: ConfigEditorState) => void;
  onSubmit?: () => void;
  submitLabel?: string;
  showSubmit?: boolean;
}

export function ConfigEditor({ initial, onChange, onSubmit, submitLabel = 'Save & push', showSubmit = false }: Props) {
  const [state, setState] = useState<ConfigEditorState>(initial);
  const [newIface, setNewIface] = useState('');

  const update = (patch: Partial<ConfigEditorState>) => {
    const next = { ...state, ...patch };
    setState(next);
    onChange(next);
  };

  const addInterface = () => {
    if (!newIface.trim()) return;
    update({ interfaces: [...state.interfaces, newIface.trim()] });
    setNewIface('');
  };

  return (
    <Stack>
      <Switch label="Scan WiFi networks" checked={state.scanWifi} onChange={(e) => update({ scanWifi: e.currentTarget.checked })} />
      <Switch label="Scan Bluetooth" checked={state.scanBluetooth} onChange={(e) => update({ scanBluetooth: e.currentTarget.checked })} />
      <Switch label="Scan BLE" checked={state.scanBtLe} onChange={(e) => update({ scanBtLe: e.currentTarget.checked })} />
      <Switch
        label="Detect clients (promiscuous, monitor mode)"
        checked={state.scanClientsPromiscuous}
        onChange={(e) => update({ scanClientsPromiscuous: e.currentTarget.checked })}
      />
      <NumberInput label="Channel dwell (ms)" value={state.channelHopMs} min={50} step={50}
        onChange={(v) => update({ channelHopMs: Number(v) || 500 })} />
      <NumberInput label="Batch interval (ms)" value={state.batchIntervalMs} min={200} step={100}
        onChange={(v) => update({ batchIntervalMs: Number(v) || 1500 })} />

      <Stack gap="xs">
        <TextInput label="Interfaces (monitor mode)" placeholder="wlan0"
          value={newIface} onChange={(e) => setNewIface(e.currentTarget.value)}
          rightSection={<Button size="compact-xs" onClick={addInterface}>Add</Button>} />
        <Select
          data={state.interfaces}
          value={state.interfaces.length ? state.interfaces[state.interfaces.length - 1] : null}
          onChange={(name) => {
            if (name) update({ interfaces: state.interfaces.filter((i) => i !== name) });
          }}
          placeholder="Click an interface to remove"
          clearable
        />
      </Stack>

      {showSubmit && onSubmit && (
        <Button onClick={onSubmit}>{submitLabel}</Button>
      )}
    </Stack>
  );
}
