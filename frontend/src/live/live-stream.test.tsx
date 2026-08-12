import { describe, expect, it } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import type { LiveEvent } from '@/lib/domain';
import type { LiveStreamSource } from './source';
import { LiveStreamProvider } from './LiveStreamProvider';
import { useGetLiveSessionQuery } from './liveApi';
import { renderWithProviders } from '@/test/utils';
import { getDb } from '@/mocks/db';

class FakeSource implements LiveStreamSource {
  listeners = new Set<(e: LiveEvent) => void>();
  subscribe(_sessionId: string, cb: (e: LiveEvent) => void): () => void {
    this.listeners.add(cb);
    return () => this.listeners.delete(cb);
  }
  close(): void {
    this.listeners.clear();
  }
  emit(e: LiveEvent): void {
    for (const l of this.listeners) l(e);
  }
}

function Harness({ sessionId }: { sessionId: string }) {
  const { data } = useGetLiveSessionQuery(sessionId);
  return (
    <div>
      <span data-testid="points">{Object.keys(data?.points ?? {}).length}</span>
      <span data-testid="unlocated">{data?.unlocated.length ?? 0}</span>
      <span data-testid="batches">{data?.batchCount ?? 0}</span>
      <span data-testid="online">{Object.values(data?.devices ?? {}).filter((d) => d.status === 'online').length}</span>
    </div>
  );
}

describe('LiveStreamProvider → RTK Query cache', () => {
  it('routes synthetic detections into the cache, distinguishing located from unlocated', async () => {
    const db = getDb();
    const session = db.sessions.find((s) => s.status === 'active')!;
    const source = new FakeSource();

    renderWithProviders(
      <LiveStreamProvider sessionId={session.id} source={source}>
        <Harness sessionId={session.id} />
      </LiveStreamProvider>,
      { sessionId: session.id },
    );

    // Wait for the initial REST snapshot to land (points > 0 for an active session).
    await waitFor(() => {
      expect(Number(screen.getByTestId('points').textContent)).toBeGreaterThan(0);
    });
    const initialPoints = Number(screen.getByTestId('points').textContent);
    expect(screen.getByTestId('batches')).toHaveTextContent('0');
    expect(screen.getByTestId('unlocated')).toHaveTextContent('0');

    const now = new Date().toISOString();
    act(() => {
      source.emit({
        type: 'detections',
        sessionId: session.id,
        batchId: 'mock-batch-1',
        deviceId: 'd1',
        detections: [
          {
            mac: 'AA:BB:CC:DD:EE:AA',
            deviceType: 'AP',
            ssid: 'LocatedNet',
            btName: null,
            signalDbm: -60,
            channel: 6,
            encryption: 'wpa2',
            detectedAt: now,
            locationFlag: 'gps',
            lat: 48.1,
            lon: 11.5,
            vendorName: null,
          },
          {
            mac: 'AA:BB:CC:DD:EE:BB',
            deviceType: 'BT_LE',
            ssid: null,
            btName: 'AirTag',
            signalDbm: -70,
            channel: 38,
            encryption: 'unspecified',
            detectedAt: now,
            locationFlag: 'inferred',
            lat: 48.11,
            lon: 11.51,
            vendorName: null,
          },
          {
            mac: 'AA:BB:CC:DD:EE:CC',
            deviceType: 'CLIENT',
            ssid: null,
            btName: null,
            signalDbm: -80,
            channel: 1,
            encryption: 'unspecified',
            detectedAt: now,
            locationFlag: 'unlocated',
            lat: null,
            lon: null,
            vendorName: null,
          },
        ],
      });
    });

    // Two new located land as points; the unlocated one stays out.
    await waitFor(() => {
      expect(screen.getByTestId('points')).toHaveTextContent(String(initialPoints + 2));
    });
    expect(screen.getByTestId('unlocated')).toHaveTextContent('1');
    expect(screen.getByTestId('batches')).toHaveTextContent('1');
  });

  it('emits a fleet event into device heartbeats', async () => {
    const db = getDb();
    const session = db.sessions.find((s) => s.status === 'active')!;
    const source = new FakeSource();

    renderWithProviders(
      <LiveStreamProvider sessionId={session.id} source={source}>
        <Harness sessionId={session.id} />
      </LiveStreamProvider>,
      { sessionId: session.id },
    );

    await waitFor(() => {
      expect(Number(screen.getByTestId('online').textContent)).toBeGreaterThan(0);
    });
    const initialOnline = Number(screen.getByTestId('online').textContent);

    act(() => {
      source.emit({
        type: 'device',
        sessionId: session.id,
        heartbeat: {
          deviceId: 'fake-device',
          at: new Date().toISOString(),
          status: 'online',
          currentChannel: 11,
          batteryPct: 90,
          gpsFix: true,
          detectionsBuffered: 0,
          detectionsSentTotal: 1,
        },
      });
    });

    await waitFor(() => {
      expect(screen.getByTestId('online')).toHaveTextContent(String(initialOnline + 1));
    });
  });
});
