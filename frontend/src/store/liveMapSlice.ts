import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import type { LocatedBatch, LocatedDetection } from '../generated/detection_pb';

export interface MapPoint {
  mac: string;
  lat: number;
  lon: number;
  type: string;
  ssid?: string;
  signalDbm: number;
  lastSeen: number;
}

export interface LiveMapState {
  points: Record<string, MapPoint>;
  batchCount: number;
  lastBatchAt: number | null;
}

export const initialState: LiveMapState = { points: {}, batchCount: 0, lastBatchAt: null };

export const DEVICE_TYPE_NAMES = ['UNSPECIFIED', 'AP', 'BLUETOOTH', 'BT_LE', 'CLIENT'] as const;

/** Maps a located detection to a map point, or null if it has no position. */
export function toMapPoint(detection: LocatedDetection): MapPoint | null {
  const d = detection.detection;
  if (!d || !d.mac) return null;
  if (detection.locationFlag === 0 || detection.locationFlag === 3) return null; // UNSPECIFIED/UNLOCATED
  return {
    mac: d.mac,
    lat: detection.lat,
    lon: detection.lon,
    type: DEVICE_TYPE_NAMES[d.deviceType] ?? 'UNKNOWN',
    ssid: d.ssid || undefined,
    signalDbm: d.signalDbm,
    lastSeen: Date.now(),
  };
}

const liveMapSlice = createSlice({
  name: 'liveMap',
  initialState,
  reducers: {
    batchReceived(state, action: PayloadAction<LocatedBatch>) {
      const batch = action.payload;
      for (const detection of batch.detections) {
        const point = toMapPoint(detection);
        if (point) state.points[point.mac] = point;
      }
      state.batchCount += 1;
      state.lastBatchAt = Date.now();
    },
    reset(state) {
      state.points = {};
      state.batchCount = 0;
      state.lastBatchAt = null;
    },
  },
});

export const { batchReceived, reset } = liveMapSlice.actions;
export default liveMapSlice.reducer;
