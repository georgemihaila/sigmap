import type { ScanConfig } from '../generated/config_pb';

export interface ConfigFieldDiff {
  field: string;
  from: unknown;
  to: unknown;
}

export type { ScanConfig };

const scalarFields: Array<keyof ScanConfig> = [
  'channelHopMs',
  'scanWifi',
  'scanBluetooth',
  'scanBtLe',
  'scanClientsPromiscuous',
  'batchIntervalMs',
];

/**
 * Produces a human-readable diff between two scan configs. Used by the preset
 * manager and the device fleet view to preview what a config change will do.
 */
export function diffScanConfig(a: ScanConfig, b: ScanConfig): ConfigFieldDiff[] {
  const diffs: ConfigFieldDiff[] = [];

  for (const field of scalarFields) {
    if (a[field] !== b[field]) {
      diffs.push({ field: field as string, from: a[field], to: b[field] });
    }
  }

  const aNames = (a.interfaces ?? []).map((i) => i.name).sort();
  const bNames = (b.interfaces ?? []).map((i) => i.name).sort();
  if (JSON.stringify(aNames) !== JSON.stringify(bNames)) {
    diffs.push({ field: 'interfaces', from: a.interfaces, to: b.interfaces });
  }

  return diffs;
}
