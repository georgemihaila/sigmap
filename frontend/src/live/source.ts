import type { LiveEvent } from '@/lib/domain';
import type { LiveSessionState } from '@/lib/domain';
import { GrpcWebLiveStreamSource } from './grpcWebSource';

/**
 * Live-update seam. The browser app consumes a stream of `LiveEvent`s shaped
 * like the BFF's gRPC-Web `LiveStream.Subscribe` oneof, streamed from the
 * ASP.NET backend and proxied by Vite in dev.
 */
export interface LiveStreamSource {
  subscribe(sessionId: string, listener: (event: LiveEvent) => void): () => void;
  close(): void;
}

export function createLiveStreamSource(): LiveStreamSource {
  return new GrpcWebLiveStreamSource();
}

const MAX_UNLOCATED = 20;

/**
 * Merge a live event into a `LiveSessionState` cache entry. Pure and unit
 * testable: located detections land in `points`; unlocated detections go to
 * `unlocated` and never fabricate a position.
 */
export function applyLiveEvent(
  draft: LiveSessionState,
  event: LiveEvent,
): LiveSessionState {
  switch (event.type) {
    case 'detections': {
      for (const d of event.detections) {
        const located = (d.locationFlag === 'gps' || d.locationFlag === 'inferred') && d.lat != null && d.lon != null;
        if (located) {
          draft.points[d.mac] = {
            mac: d.mac,
            lat: d.lat as number,
            lon: d.lon as number,
            type: d.deviceType,
            ssid: d.ssid,
            signalDbm: d.signalDbm,
            lastSeen: d.detectedAt,
          };
        } else {
          const existing = draft.unlocated.findIndex((u) => u.mac === d.mac);
          if (existing >= 0) draft.unlocated.splice(existing, 1);
          draft.unlocated.push(d);
          if (draft.unlocated.length > MAX_UNLOCATED) draft.unlocated.shift();
        }
      }
      draft.batchCount += 1;
      draft.lastBatchAt = new Date().toISOString();
      break;
    }
    case 'device': {
      draft.devices[event.heartbeat.deviceId] = event.heartbeat;
      break;
    }
    case 'fleet': {
      for (const hb of event.devices) draft.devices[hb.deviceId] = hb;
      break;
    }
    case 'config':
      break;
  }
  return draft;
}
