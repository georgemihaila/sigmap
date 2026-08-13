import { GrpcWebFetchTransport } from '@protobuf-ts/grpcweb-transport';
import { stackIntercept } from '@protobuf-ts/runtime-rpc';
import type {
  Heartbeat as ProtoHeartbeat,
  LiveEvent as ProtoLiveEvent,
  LocatedDetection as ProtoLocatedDetection,
  SubscribeRequest,
} from './generated/live';
import { LiveStream } from './generated/live';
import type { LiveStreamSource } from './source';
import type {
  DeviceType,
  Encryption,
  LiveEvent,
  LocationFlag,
  PushState,
} from '@/lib/domain';

/**
 * Real gRPC-Web live transport: streams the backend `LiveStream.Subscribe`
 * server-stream (binary-protobuf over HTTP/1.1, proxied by Vite in dev) and
 * maps each event into the same `LiveEvent` shape the mock emitter produced.
 */
export class GrpcWebLiveStreamSource implements LiveStreamSource {
  private readonly transport: GrpcWebFetchTransport;
  private readonly iterators = new Set<AsyncIterator<ProtoLiveEvent>>();

  constructor(baseUrl?: string) {
    const origin =
      baseUrl ??
      (typeof window !== 'undefined' && window.location?.origin ? window.location.origin : '');
    this.transport = new GrpcWebFetchTransport({
      baseUrl: origin,
      format: 'text',
      credentials: 'include',
    });
  }

  subscribe(sessionId: string, listener: (event: LiveEvent) => void): () => void {
    const method = LiveStream.methods[0];
    const call = stackIntercept<SubscribeRequest, ProtoLiveEvent>(
      'serverStreaming',
      this.transport,
      method,
      {},
      { sessionId },
    );
    const iterator = call.responses[Symbol.asyncIterator]();
    this.iterators.add(iterator);

    const run = async () => {
      try {
        for (;;) {
          const { done, value } = await iterator.next();
          if (done) break;
          listener(toDomainEvent(value));
        }
      } catch (err) {
        console.error('[live] stream error', err);
      }
    };
    void run();

    return () => {
      void iterator.return?.();
      this.iterators.delete(iterator);
    };
  }

  close(): void {
    for (const iterator of this.iterators) void iterator.return?.();
    this.iterators.clear();
  }
}

function toDomainEvent(e: ProtoLiveEvent): LiveEvent {
  const p = e.payload;
  switch (p.oneofKind) {
    case 'detections':
      return {
        type: 'detections',
        sessionId: e.sessionId,
        batchId: p.detections.batchId,
        deviceId: p.detections.deviceId,
        detections: p.detections.detections.map(toLocated),
      };
    case 'device':
      return {
        type: 'device',
        sessionId: e.sessionId,
        heartbeat: toHeartbeat(p.device),
      };
    case 'config':
      return {
        type: 'config',
        sessionId: e.sessionId,
        deviceId: p.config.deviceId,
        pushId: p.config.pushId,
        status: p.config.status as PushState,
      };
    case 'fleet':
      return {
        type: 'fleet',
        sessionId: e.sessionId,
        devices: p.fleet.devices.map(toHeartbeat),
      };
    default:
      throw new Error('unknown live event payload');
  }
}

function toLocated(d: ProtoLocatedDetection): {
  mac: string;
  deviceType: DeviceType;
  ssid: string | null;
  btName: string | null;
  signalDbm: number;
  channel: number;
  encryption: Encryption;
  detectedAt: string;
  locationFlag: LocationFlag;
  lat: number | null;
  lon: number | null;
  vendorName: string | null;
} {
  return {
    mac: d.mac,
    deviceType: d.deviceType as DeviceType,
    ssid: d.ssid ?? null,
    btName: d.btName ?? null,
    signalDbm: d.signalDbm,
    channel: d.channel,
    encryption: d.encryption as Encryption,
    detectedAt: d.detectedAt,
    locationFlag: d.locationFlag as LocationFlag,
    lat: d.lat ?? null,
    lon: d.lon ?? null,
    vendorName: d.vendorName ?? null,
  };
}

function toHeartbeat(h: ProtoHeartbeat): {
  deviceId: string;
  at: string;
  status: 'online' | 'busy' | 'error';
  currentChannel: number | null;
  batteryPct: number | null;
  gpsFix: boolean;
  detectionsBuffered: number;
  detectionsSentTotal: number;
} {
  return {
    deviceId: h.deviceId,
    at: h.at,
    status: (h.status as 'online' | 'busy' | 'error') ?? 'online',
    currentChannel: h.currentChannel ?? null,
    batteryPct: h.batteryPct ?? null,
    gpsFix: h.gpsFix,
    detectionsBuffered: h.detectionsBuffered,
    detectionsSentTotal: h.detectionsSentTotal,
  };
}
