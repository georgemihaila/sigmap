import { describe, expect, it } from 'vitest';
import type { LiveSessionState } from '@/lib/domain';
import { applyLiveEvent } from './source';

function makeState(): LiveSessionState {
  return {
    sessionId: 's1',
    points: {},
    unlocated: [],
    batchCount: 0,
    lastBatchAt: null,
    devices: {},
  };
}

function detection(overrides: Partial<import('@/lib/domain').LocatedDetection> = {}) {
  return {
    mac: 'AA:BB:CC:DD:EE:01',
    deviceType: 'AP',
    ssid: 'TestNet',
    btName: null,
    signalDbm: -55,
    channel: 6,
    encryption: 'wpa2',
    detectedAt: new Date().toISOString(),
    locationFlag: 'gps',
    lat: 48.1,
    lon: 11.5,
    vendorName: 'Intel',
    ...overrides,
  } as const;
}

describe('applyLiveEvent', () => {
  it('lands located detections into points', () => {
    const draft = makeState();
    applyLiveEvent(draft, {
      type: 'detections',
      sessionId: 's1',
      batchId: 'b1',
      deviceId: 'd1',
      detections: [detection()],
    });
    expect(draft.points['AA:BB:CC:DD:EE:01']).toMatchObject({
      lat: 48.1,
      lon: 11.5,
      type: 'AP',
      ssid: 'TestNet',
      signalDbm: -55,
    });
    expect(draft.unlocated).toHaveLength(0);
    expect(draft.batchCount).toBe(1);
  });

  it('keeps unlocated detections out of points (no fabricated position)', () => {
    const draft = makeState();
    applyLiveEvent(draft, {
      type: 'detections',
      sessionId: 's1',
      batchId: 'b1',
      deviceId: 'd1',
      detections: [detection({ locationFlag: 'unlocated', lat: null, lon: null })],
    });
    expect(Object.keys(draft.points)).toHaveLength(0);
    expect(draft.unlocated).toHaveLength(1);
    expect(draft.unlocated[0].mac).toBe('AA:BB:CC:DD:EE:01');
  });

  it('replaces a located point when the same device is re-seen unlocated elsewhere; keeps last located when only unlocated', () => {
    const draft = makeState();
    applyLiveEvent(draft, {
      type: 'detections',
      sessionId: 's1',
      batchId: 'b1',
      deviceId: 'd1',
      detections: [detection({ mac: 'AA:BB:CC:DD:EE:02', lat: 48.2, lon: 11.6 })],
    });
    expect(draft.points['AA:BB:CC:DD:EE:02'].lat).toBe(48.2);

    // Same MAC comes back unlocated — the located point stays, the unlocated
    // sighting is recorded separately.
    applyLiveEvent(draft, {
      type: 'detections',
      sessionId: 's1',
      batchId: 'b2',
      deviceId: 'd1',
      detections: [detection({ mac: 'AA:BB:CC:DD:EE:02', locationFlag: 'unlocated', lat: null, lon: null })],
    });
    expect(draft.points['AA:BB:CC:DD:EE:02'].lat).toBe(48.2);
    expect(draft.unlocated.map((u) => u.mac)).toEqual(['AA:BB:CC:DD:EE:02']);
  });

  it('updates a point when a device is re-seen at a new location', () => {
    const draft = makeState();
    applyLiveEvent(draft, {
      type: 'detections',
      sessionId: 's1',
      batchId: 'b1',
      deviceId: 'd1',
      detections: [detection({ mac: 'AA:BB:CC:DD:EE:03', lat: 1, lon: 1 })],
    });
    applyLiveEvent(draft, {
      type: 'detections',
      sessionId: 's1',
      batchId: 'b2',
      deviceId: 'd1',
      detections: [detection({ mac: 'AA:BB:CC:DD:EE:03', lat: 2, lon: 2 })],
    });
    expect(draft.points['AA:BB:CC:DD:EE:03'].lat).toBe(2);
    expect(draft.batchCount).toBe(2);
  });

  it('merges device heartbeats', () => {
    const draft = makeState();
    applyLiveEvent(draft, {
      type: 'device',
      sessionId: 's1',
      heartbeat: {
        deviceId: 'd1',
        at: new Date().toISOString(),
        status: 'online',
        currentChannel: 6,
        batteryPct: 80,
        gpsFix: true,
        detectionsBuffered: 3,
        detectionsSentTotal: 100,
      },
    });
    expect(draft.devices.d1.status).toBe('online');
  });
});
