import type { LiveEvent } from '@/lib/domain';
import type { LiveStreamSource } from '@/live/source';
import { appendLiveBatch, getDb } from './db';

const BATCH_INTERVAL_MS = 1200;

/**
 * Mock live transport: an interval emitter that generates synthetic detection
 * batches, heartbeats and config acks and pushes them into the fixture db and
 * out to subscribers — the same `LiveEvent` contract the real BFF stream will
 * use. Components never see this class directly.
 */
export class MockLiveStreamSource implements LiveStreamSource {
  private listeners = new Map<string, Set<(event: LiveEvent) => void>>();
  private timers = new Map<string, ReturnType<typeof setInterval>>();

  subscribe(sessionId: string, listener: (event: LiveEvent) => void): () => void {
    let set = this.listeners.get(sessionId);
    if (!set) {
      set = new Set();
      this.listeners.set(sessionId, set);
    }
    set.add(listener);

    // Initial fleet snapshot so the UI has device state before any batches.
    const db = getDb();
    const fleet = db.sessionDevices
      .filter((sd) => sd.sessionId === sessionId)
      .map((sd) => {
        const device = db.devices.find((d) => d.id === sd.deviceId);
        return {
          deviceId: sd.deviceId,
          at: device?.lastHeartbeatAt ?? new Date().toISOString(),
          status: device?.status === 'error' ? ('error' as const) : device?.status === 'online' ? ('online' as const) : ('online' as const),
          currentChannel: device?.status === 'online' ? 6 : null,
          batteryPct: device?.capabilities.hasBattery ? 78 : null,
          gpsFix: device?.capabilities.hasGps ?? false,
          detectionsBuffered: 0,
          detectionsSentTotal: 0,
        };
      });
    listener({ type: 'fleet', sessionId, devices: fleet });

    if (!this.timers.has(sessionId)) {
      const timer = setInterval(() => {
        const db = getDb();
        const members = db.sessionDevices.filter((sd) => sd.sessionId === sessionId);
        const online = members.filter((sd) => db.devices.find((d) => d.id === sd.deviceId)?.status === 'online');
        if (online.length === 0) return;
        const member = online[Math.floor(Math.random() * online.length)];
        const count = Math.floor(Math.random() * 6) + 1;
        const events = appendLiveBatch(sessionId, member.deviceId, count);
        const set = this.listeners.get(sessionId);
        if (!set) return;
        for (const event of events) {
          for (const l of set) l(event);
        }
        // Occasionally push a heartbeat / config state change.
        if (Math.random() < 0.25) {
          const hb = db.devices.find((d) => d.id === member.deviceId);
          const ev: LiveEvent = {
            type: 'device',
            sessionId,
            heartbeat: {
              deviceId: member.deviceId,
              at: new Date().toISOString(),
              status: 'online',
              currentChannel: Math.floor(Math.random() * 11) + 1,
              batteryPct: hb?.capabilities.hasBattery ? Math.floor(Math.random() * 60) + 40 : null,
              gpsFix: hb?.capabilities.hasGps ?? false,
              detectionsBuffered: Math.floor(Math.random() * 50),
              detectionsSentTotal: Math.floor(Math.random() * 4000) + 1000,
            },
          };
          for (const l of set) l(ev);
        }
      }, BATCH_INTERVAL_MS);
      this.timers.set(sessionId, timer);
    }

    return () => {
      set!.delete(listener);
      if (set!.size === 0) {
        const timer = this.timers.get(sessionId);
        if (timer) clearInterval(timer);
        this.timers.delete(sessionId);
      }
    };
  }

  close(): void {
    for (const timer of this.timers.values()) clearInterval(timer);
    this.timers.clear();
    this.listeners.clear();
  }
}
