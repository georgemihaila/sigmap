import type { ConfigChange, ScanConfig } from './domain';

/** The built-in "Off" config every newly paired device starts on. */
export function offScanConfig(): ScanConfig {
  return {
    interfaces: [],
    channelHopMs: 0,
    scanWifi: false,
    scanBluetooth: false,
    scanBtLe: false,
    scanClientsPromiscuous: false,
    batchIntervalMs: 1500,
  };
}

export function defaultScanConfig(): ScanConfig {
  return {
    interfaces: [
      { name: 'wlan0', driver: 'nl80211', monitorMode: true, enabled: true, channels: [] },
    ],
    channelHopMs: 500,
    scanWifi: true,
    scanBluetooth: false,
    scanBtLe: true,
    scanClientsPromiscuous: false,
    batchIntervalMs: 1500,
  };
}

export function parseScanConfig(json: string | null | undefined): ScanConfig {
  if (!json) return offScanConfig();
  try {
    const parsed = JSON.parse(json) as Partial<ScanConfig>;
    return normalizeScanConfig(parsed);
  } catch {
    return offScanConfig();
  }
}

export function normalizeScanConfig(raw: Partial<ScanConfig>): ScanConfig {
  const base = offScanConfig();
  return {
    interfaces: Array.isArray(raw.interfaces) ? raw.interfaces : base.interfaces,
    channelHopMs: raw.channelHopMs ?? base.channelHopMs,
    scanWifi: raw.scanWifi ?? base.scanWifi,
    scanBluetooth: raw.scanBluetooth ?? base.scanBluetooth,
    scanBtLe: raw.scanBtLe ?? base.scanBtLe,
    scanClientsPromiscuous: raw.scanClientsPromiscuous ?? base.scanClientsPromiscuous,
    batchIntervalMs: raw.batchIntervalMs ?? base.batchIntervalMs,
  };
}

export function stringifyScanConfig(config: ScanConfig): string {
  return JSON.stringify(config);
}

function fmt(v: unknown): string {
  return typeof v === 'string' ? v : JSON.stringify(v);
}

/**
 * Compute the discrete list of changes between two scan configs.
 * Used by the bulk config preview and unit-tested directly.
 */
export function diffScanConfigs(before: ScanConfig, after: ScanConfig): ConfigChange[] {
  const changes: ConfigChange[] = [];

  const beforeIfaces = new Map(before.interfaces.map((i) => [i.name, i]));
  const afterIfaces = new Map(after.interfaces.map((i) => [i.name, i]));

  for (const [name] of beforeIfaces) {
    if (!afterIfaces.has(name)) {
      changes.push({ path: `interfaces[${name}]`, before: 'present', after: 'removed' });
    }
  }
  for (const [name, a] of afterIfaces) {
    const b = beforeIfaces.get(name);
    if (!b) {
      changes.push({ path: `interfaces[${name}]`, before: 'absent', after: 'added' });
      continue;
    }
    if (a.enabled !== b.enabled) {
      changes.push({ path: `interfaces[${name}].enabled`, before: String(b.enabled), after: String(a.enabled) });
    }
    if (a.monitorMode !== b.monitorMode) {
      changes.push({
        path: `interfaces[${name}].monitorMode`,
        before: String(b.monitorMode),
        after: String(a.monitorMode),
      });
    }
    if (a.driver !== b.driver) {
      changes.push({ path: `interfaces[${name}].driver`, before: b.driver, after: a.driver });
    }
    if (a.channels.join(',') !== b.channels.join(',')) {
      changes.push({
        path: `interfaces[${name}].channels`,
        before: b.channels.length ? b.channels.join(',') : 'all',
        after: a.channels.length ? a.channels.join(',') : 'all',
      });
    }
  }

  const scalars: Array<[keyof ScanConfig, string]> = [
    ['channelHopMs', 'channelHopMs'],
    ['batchIntervalMs', 'batchIntervalMs'],
    ['scanWifi', 'scanWifi'],
    ['scanBluetooth', 'scanBluetooth'],
    ['scanBtLe', 'scanBtLe'],
    ['scanClientsPromiscuous', 'scanClientsPromiscuous'],
  ];
  for (const [key, label] of scalars) {
    const b = before[key];
    const a = after[key];
    if (b !== a) {
      changes.push({ path: label, before: fmt(b), after: fmt(a) });
    }
  }

  return changes;
}

/** Merge the interfaces a device currently reports into a target config. */
export function mergeInterfaces(target: ScanConfig, currentNames: string[]): ScanConfig {
  if (currentNames.length === 0 || target.interfaces.length > 0) return target;
  return {
    ...target,
    interfaces: currentNames.map((name) => ({
      name,
      driver: 'nl80211',
      monitorMode: true,
      enabled: true,
      channels: [],
    })),
  };
}
