import { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useAppDispatch } from '@/hooks';
import { applyLiveEvent, createLiveStreamSource, type LiveStreamSource } from './source';
import { liveApi } from './liveApi';

interface LiveStreamContextValue {
  connected: boolean;
  sessionId: string | null;
}

const LiveStreamContext = createContext<LiveStreamContextValue>({
  connected: false,
  sessionId: null,
});

export function useLiveStream() {
  return useContext(LiveStreamContext);
}

interface LiveStreamProviderProps {
  sessionId: string | null;
  /** Test seam: inject a deterministic source instead of the interval emitter. */
  source?: LiveStreamSource;
  children: React.ReactNode;
}

/**
 * Subscribes to the live stream for `sessionId` and routes each event into the
 * RTK Query cache entry for `getLiveSession(sessionId)`. Components read live
 * state through the normal query hook and never touch the transport.
 */
export function LiveStreamProvider({ sessionId, source, children }: LiveStreamProviderProps) {
  const [connected, setConnected] = useState(false);
  const sourceRef = useRef<LiveStreamSource | null>(null);
  const dispatch = useAppDispatch();

  useEffect(() => {
    if (!sessionId) {
      setConnected(false);
      return;
    }
    const src = source ?? createLiveStreamSource();
    sourceRef.current = src;
    const unsubscribe = src.subscribe(sessionId, (event) => {
      dispatch(
        liveApi.util.updateQueryData('getLiveSession', sessionId, (draft) => {
          applyLiveEvent(draft, event);
        }),
      );
    });
    setConnected(true);
    return () => {
      unsubscribe();
      src.close();
      setConnected(false);
    };
  }, [sessionId, source, dispatch]);

  const value = useMemo(() => ({ connected, sessionId }), [connected, sessionId]);

  return <LiveStreamContext.Provider value={value}>{children}</LiveStreamContext.Provider>;
}
