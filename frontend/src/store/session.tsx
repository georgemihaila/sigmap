import { createContext, useContext, useState, type ReactNode } from 'react';

interface SessionContextValue {
  activeSessionId: string | null;
  setActiveSessionId: (id: string | null) => void;
}

const SessionContext = createContext<SessionContextValue>({
  activeSessionId: null,
  setActiveSessionId: () => {},
});

export function SessionProvider({ children }: { children: ReactNode }) {
  const [activeSessionId, setActiveSessionId] = useState<string | null>(null);
  return (
    <SessionContext.Provider value={{ activeSessionId, setActiveSessionId }}>
      {children}
    </SessionContext.Provider>
  );
}

export function useSession() {
  return useContext(SessionContext);
}
