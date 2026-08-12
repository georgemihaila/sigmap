import { configureStore } from '@reduxjs/toolkit';
import { setupListeners } from '@reduxjs/toolkit/query';

import { api } from '@/api/baseApi';
// Import slice modules so their `injectEndpoints` registration runs.
import '@/api/authApi';
import '@/api/sessionsApi';
import '@/api/fleetApi';
import '@/api/presetsApi';
import '@/api/detectedApi';
import '@/api/configApi';
import '@/api/exportsApi';
import '@/api/pairingApi';
import '@/live/liveApi';

export const store = configureStore({
  reducer: {
    [api.reducerPath]: api.reducer,
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(api.middleware),
});

setupListeners(store.dispatch);

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
