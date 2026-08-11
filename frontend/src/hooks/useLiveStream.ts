import { createClient } from '@connectrpc/connect';
import { createGrpcWebTransport } from '@connectrpc/connect-web';
import { useEffect } from 'react';
import { useAppDispatch } from '../store/hooks';
import { LiveStream } from '../generated/live_connect';
import { batchReceived } from '../store/liveMapSlice';

let transport: ReturnType<typeof createGrpcWebTransport> | null = null;
function getTransport() {
  if (!transport) {
    // Same-origin in prod; VITE_BFF_URL points at the BFF in dev when it's not
    // served from the same origin.
    const baseUrl = import.meta.env.VITE_BFF_URL ?? window.location.origin;
    transport = createGrpcWebTransport({ baseUrl });
  }
  return transport;
}

/**
 * Subscribes to the BFF's gRPC-Web live stream and dispatches located batches
 * into the live-map slice. Reconnects on error/close.
 */
export function useLiveStream(sessionId: string | null) {
  const dispatch = useAppDispatch();

  useEffect(() => {
    let closed = false;
    let retryTimer: ReturnType<typeof setTimeout> | null = null;

    const connect = async () => {
      if (closed) return;
      try {
        const client = createClient(LiveStream, getTransport());
        const stream = client.subscribe({
          sessionId: sessionId ?? '',
          clientId: crypto.randomUUID(),
        });
        for await (const event of stream) {
          if (event.event.case === 'detections') {
            dispatch(batchReceived(event.event.value));
          }
        }
        if (!closed) retryTimer = setTimeout(connect, 3000);
      } catch {
        if (!closed) retryTimer = setTimeout(connect, 3000);
      }
    };

    void connect();
    return () => {
      closed = true;
      if (retryTimer) clearTimeout(retryTimer);
    };
  }, [dispatch, sessionId]);
}
