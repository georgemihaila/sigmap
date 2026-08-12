import { createContext, useContext } from 'react';

export const SessionContext = createContext<string | null>(null);

export function useSessionId(): string | null {
  return useContext(SessionContext);
}
