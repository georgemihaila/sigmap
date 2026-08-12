import type { ReactElement, ReactNode } from 'react';
import { render } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter } from 'react-router-dom';
import { configureStore } from '@reduxjs/toolkit';
import { api } from '@/api/baseApi';
import { ThemeProvider } from '@/components/theme-provider';
import { TooltipProvider } from '@/components/ui/tooltip';
import { Toaster } from '@/components/ui/sonner';
import { SessionContext } from '@/features/sessions/session-context';
import '@/store';

function makeStore() {
  return configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (gdm) => gdm().concat(api.middleware),
  });
}

export function renderWithProviders(
  ui: ReactElement,
  options: { sessionId?: string | null; route?: string } = {},
) {
  const store = makeStore();
  const wrapped = (
    <Provider store={store}>
      <ThemeProvider>
        <TooltipProvider delayDuration={0}>
          <MemoryRouter initialEntries={options.route ? [options.route] : ['/']}>
            <SessionContext.Provider value={options.sessionId ?? null}>{ui}</SessionContext.Provider>
            <Toaster />
          </MemoryRouter>
        </TooltipProvider>
      </ThemeProvider>
    </Provider>
  );
  return {
    store,
    ...render(wrapped as ReactElement<ReactNode>),
  };
}

export * from '@testing-library/react';
