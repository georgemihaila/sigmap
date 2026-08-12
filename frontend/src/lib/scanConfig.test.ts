import { describe, expect, it } from 'vitest';
import { defaultScanConfig, diffScanConfigs, mergeInterfaces, offScanConfig, parseScanConfig } from './scanConfig';

describe('diffScanConfigs', () => {
  it('returns no changes for identical configs', () => {
    const a = defaultScanConfig();
    const b = parseScanConfig(JSON.stringify(a));
    expect(diffScanConfigs(a, b)).toEqual([]);
  });

  it('detects a toggle change', () => {
    const a = defaultScanConfig();
    const b = { ...a, scanBluetooth: !a.scanBluetooth };
    const changes = diffScanConfigs(a, b);
    expect(changes).toHaveLength(1);
    expect(changes[0]).toMatchObject({
      path: 'scanBluetooth',
      before: String(a.scanBluetooth),
      after: String(b.scanBluetooth),
    });
  });

  it('detects interface add/remove', () => {
    const a = defaultScanConfig();
    const b = { ...a, interfaces: [{ ...a.interfaces[0] }, { ...a.interfaces[0], name: 'wlan1' }] };
    const changes = diffScanConfigs(a, b);
    expect(changes).toContainEqual({ path: 'interfaces[wlan1]', before: 'absent', after: 'added' });

    const c = diffScanConfigs(b, a);
    expect(c).toContainEqual({ path: 'interfaces[wlan1]', before: 'present', after: 'removed' });
  });

  it('detects interface property and channel changes', () => {
    const a = defaultScanConfig();
    const b = {
      ...a,
      interfaces: [{ ...a.interfaces[0], monitorMode: false, channels: [1, 6, 11] }],
    };
    const changes = diffScanConfigs(a, b);
    expect(changes).toEqual(
      expect.arrayContaining([
        { path: 'interfaces[wlan0].monitorMode', before: 'true', after: 'false' },
        { path: 'interfaces[wlan0].channels', before: 'all', after: '1,6,11' },
      ]),
    );
  });

  it('detects scalar changes', () => {
    const a = defaultScanConfig();
    const b = { ...a, channelHopMs: 900, batchIntervalMs: 2500 };
    const changes = diffScanConfigs(a, b);
    expect(changes).toEqual(
      expect.arrayContaining([
        { path: 'channelHopMs', before: '500', after: '900' },
        { path: 'batchIntervalMs', before: '1500', after: '2500' },
      ]),
    );
  });
});

describe('parseScanConfig', () => {
  it('falls back to the Off config on invalid input', () => {
    expect(parseScanConfig('not json')).toEqual(offScanConfig());
    expect(parseScanConfig(null)).toEqual(offScanConfig());
    expect(parseScanConfig(undefined)).toEqual(offScanConfig());
  });

  it('normalizes a partial config', () => {
    const cfg = parseScanConfig(JSON.stringify({ channelHopMs: 700 }));
    expect(cfg.channelHopMs).toBe(700);
    expect(cfg.scanWifi).toBe(false);
  });
});

describe('mergeInterfaces', () => {
  it('keeps existing interfaces intact', () => {
    const target = defaultScanConfig();
    expect(mergeInterfaces(target, [])).toEqual(target);
  });

  it('populates interfaces from a device when the target has none', () => {
    const target = offScanConfig();
    const merged = mergeInterfaces(target, ['wlan0', 'wlan1']);
    expect(merged.interfaces.map((i) => i.name)).toEqual(['wlan0', 'wlan1']);
    expect(merged.interfaces[0].enabled).toBe(true);
    expect(merged.interfaces[0].monitorMode).toBe(true);
  });
});
