import { describe, expect, it } from 'vitest';
import {
  DeviceType,
  Detection,
  LocatedBatch,
  LocatedDetection,
  LocationFlag,
} from '../generated/detection_pb';
import reducer, {
  batchReceived,
  initialState,
  toMapPoint,
  type LiveMapState,
} from './liveMapSlice';

function located(mac: string, lat: number, lon: number, flag: LocationFlag): LocatedDetection {
  return new LocatedDetection({
    detection: new Detection({ mac, ssid: `Net-${mac.slice(-2)}`, deviceType: DeviceType.AP, signalDbm: -55 }),
    lat,
    lon,
    locationFlag: flag,
  });
}

describe('liveMapSlice', () => {
  it('adds located detections as map points', () => {
    const batch = new LocatedBatch({ detections: [located('AA:BB:CC:DD:EE:01', 52.5, 13.4, LocationFlag.GPS)] });
    const state = reducer(initialState, batchReceived(batch));

    expect(state.points['AA:BB:CC:DD:EE:01']).toMatchObject({
      lat: 52.5,
      lon: 13.4,
      type: 'AP',
      ssid: 'Net-01',
      signalDbm: -55,
    });
    expect(state.batchCount).toBe(1);
    expect(state.lastBatchAt).not.toBeNull();
  });

  it('skips unlocated detections (never fabricates a position)', () => {
    const batch = new LocatedBatch({
      detections: [located('AA:BB:CC:DD:EE:02', 0, 0, LocationFlag.UNLOCATED)],
    });
    const state = reducer(initialState, batchReceived(batch));
    expect(state.points).toEqual({});
  });

  it('upserts by mac, keeping the latest position', () => {
    let state = reducer(
      initialState,
      batchReceived(new LocatedBatch({ detections: [located('AA:BB:CC:DD:EE:03', 52.5, 13.4, LocationFlag.GPS)] })),
    );
    state = reducer(
      state,
      batchReceived(new LocatedBatch({ detections: [located('AA:BB:CC:DD:EE:03', 52.6, 13.5, LocationFlag.GPS)] })),
    );
    expect(state.points['AA:BB:CC:DD:EE:03']).toMatchObject({ lat: 52.6, lon: 13.5 });
    expect(Object.keys(state.points)).toHaveLength(1);
  });

  it('reset clears everything', () => {
    let state: LiveMapState = reducer(
      initialState,
      batchReceived(new LocatedBatch({ detections: [located('AA:BB:CC:DD:EE:04', 1, 2, LocationFlag.GPS)] })),
    );
    state = reducer(state, { type: 'liveMap/reset' });
    expect(state.points).toEqual({});
    expect(state.batchCount).toBe(0);
  });
});

describe('toMapPoint', () => {
  it('returns null for a missing detection or empty mac', () => {
    expect(toMapPoint(new LocatedDetection({ lat: 1, lon: 2, locationFlag: LocationFlag.GPS }))).toBeNull();
  });

  it('returns null when unlocated', () => {
    expect(
      toMapPoint(located('AA:BB:CC:DD:EE:05', 1, 2, LocationFlag.UNLOCATED)),
    ).toBeNull();
  });
});
