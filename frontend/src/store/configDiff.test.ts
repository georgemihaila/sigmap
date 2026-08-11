import { describe, expect, it } from 'vitest';
import { diffScanConfig } from './configDiff';
import { InterfaceConfig, ScanConfig } from '../generated/config_pb';

function config(partial: Partial<ScanConfig> = {}): ScanConfig {
  return new ScanConfig({
    interfaces: [],
    channelHopMs: 500,
    scanWifi: false,
    scanBluetooth: false,
    scanBtLe: false,
    scanClientsPromiscuous: false,
    batchIntervalMs: 1500,
    ...partial,
  });
}

describe('diffScanConfig', () => {
  it('reports no diffs for identical configs', () => {
    const a = config();
    expect(diffScanConfig(a, new ScanConfig(a))).toEqual([]);
  });

  it('detects toggled scan types', () => {
    const diffs = diffScanConfig(config(), config({ scanWifi: true }));
    expect(diffs).toContainEqual({ field: 'scanWifi', from: false, to: true });
  });

  it('detects dwell time changes', () => {
    const diffs = diffScanConfig(config({ channelHopMs: 500 }), config({ channelHopMs: 250 }));
    expect(diffs).toContainEqual({ field: 'channelHopMs', from: 500, to: 250 });
  });

  it('detects interface list changes regardless of order', () => {
    const a = config({
      interfaces: [
        new InterfaceConfig({ name: 'wlan0' }),
        new InterfaceConfig({ name: 'wlan1' }),
      ],
    });
    const b = config({
      interfaces: [
        new InterfaceConfig({ name: 'wlan1' }),
        new InterfaceConfig({ name: 'wlan0' }),
      ],
    });
    expect(diffScanConfig(a, b)).toEqual([]);

    const c = config({ interfaces: [new InterfaceConfig({ name: 'wlan0' })] });
    expect(diffScanConfig(a, c)).toHaveLength(1);
  });
});
